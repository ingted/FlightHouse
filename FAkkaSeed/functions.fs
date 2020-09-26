module FAkkaTrade.Functions

open FAkkaTrade.Types
open P2PNET.TransportLayer
open Akkling
open Akka.Cluster.Sharding
open System
open System.Collections.Immutable
open Akka.Actor
open System.Collections.Concurrent
open P2PNET.TransportLayer.EventArgs

let getMyIp () =
    async {
        let! tt =  
            async {
                return TransportManager.GetLocalIPAddress2()
                }
        tt.Wait(200) |> ignore
        return tt.Result
        }
    |> Async.RunSynchronously

let configWithPort (pubPort:int) (port:int) (sqlserverIp:string) (sqlserverId:string) (sqlserverPwd:string) (clusterRoles:string list) (nodeIp:string) pubname ifClusterInfo =
    let config = """
        petabridge.cmd{
	                # default IP address used to listen for incoming petabridge.cmd client connections
	                # should be a safe default as it listens on "all network interfaces".
	                host = "0.0.0.0"

	                # default port number used to listen for incoming petabridge.cmd client connections
	                port = 9110

	                # when true, logs all loaded palettes on startup
	                log-palettes-on-startup = on
        }
        akka {
          stdout-loglevel = DEBUG
          loglevel = DEBUG
          #log-config-on-start = on 
          loggers = ["Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog"]
          debug {  
                receive = on 
                autoreceive = on
                lifecycle = on
                event-stream = on
                unhandled = on
            }
          actor {
        
    
            # debug.unhandled = on
        
            provider = cluster
            inbox {
                inbox-size = 100000
            }
          }
          remote {
            dot-netty.tcp {
              #byte-order = "little-endian"
              public-hostname = """ + "\"" + pubname + """"
              hostname = """ + "\"" + nodeIp + """"
              public-port = """ + pubPort.ToString() + """
              port = """ + port.ToString() + """
            }
          }""" + 
                    (
                      match ifClusterInfo with
                      | true -> """
                      cluster {
                        auto-down-unreachable-after = off
                        roles = """ + ((sprintf "%A" (clusterRoles |> List.append ["petabridge.cmd"])).Replace(";", ",")) + """
                        sharding {
                            role = "ShardNode"
                            # journal-plugin-id = "akka.persistence.journal.inmem"
                            # snapshot-plugin-id = "akka.persistence.snapshot-store.inmem"
                            journal-plugin-id = "akka.persistence.journal.sharding"
                            snapshot-plugin-id = "akka.persistence.snapshot-store.sharding"
                        }
                        #seed-nodes = [ "akka.tcp://cluster-system@localhost:8081", "akka.tcp://cluster-system@localhost:8082"  ]
                      }""" 
                      | false -> ""
                    ) + """
          persistence{
            query.journal.sql {
              max-buffer-size = 10000
            }
            journal {
              plugin = "akka.persistence.journal.sql-server"
              sql-server {
                class = "Akka.Persistence.SqlServer.Journal.BatchingSqlServerJournal, Akka.Persistence.SqlServer"
                connection-string = "Persist Security Info=False;User ID=""" + sqlserverId + """;Password=""" + sqlserverPwd + """;Initial Catalog=AkkaPersistence;Server=""" + sqlserverIp + """"
                # default SQL commands timeout
                connection-timeout = 30s
                schema-name = dbo
                table-name = journal
                metadata-table-name = metadata
                auto-initialize = off
                event-adapters {
                  json-adapter = "Sessionrapper+EventAdapter, mdcf"
                  #"Nrk.Oddjob.Upload.PersistenceUtils+EventAdapter, Upload"
                }
                event-adapter-bindings {
                  # to journal
                  "System.Object, mscorlib" = json-adapter
                  # from journal
                  "Newtonsoft.Json.Linq.JObject, Newtonsoft.Json" = [json-adapter]
                }
              }
              sharding {
                connection-string = "Persist Security Info=False;User ID=""" + sqlserverId + """;Password=""" + sqlserverPwd + """;Initial Catalog=AkkaPersistence;Server=""" + sqlserverIp + """"                   
                auto-initialize = on
                plugin-dispatcher = "akka.actor.default-dispatcher"
                class = "Akka.Persistence.SqlServer.Journal.SqlServerJournal, 
    				       Akka.Persistence.SqlServer"
                connection-timeout = 30s
                schema-name = dbo
                table-name = journal
                timestamp-provider = "Akka.Persistence.Sql.Common.Journal.DefaultTimestampProvider, 
    				       Akka.Persistence.Sql.Common"
                metadata-table-name = metadata
              }
            } 
            snapshot-store {
              plugin = "akka.persistence.snapshot-store.sql-server"
              sql-server {
                class = "Akka.Persistence.SqlServer.Snapshot.SqlServerSnapshotStore, Akka.Persistence.SqlServer"
            
                serializer = hyperion
                connection-string = "Persist Security Info=False;User ID=""" + sqlserverId + """;Password=""" + sqlserverPwd + """;Initial Catalog=AkkaPersistence;Server=""" + sqlserverIp + """"  
                # default SQL commands timeout
                connection-timeout = 30s
                schema-name = dbo
                table-name = snapshot
                auto-initialize = off
              }
              sharding {
                class = "Akka.Persistence.SqlServer.Snapshot.SqlServerSnapshotStore,
    				        Akka.Persistence.SqlServer"
                plugin-dispatcher = "akka.actor.default-dispatcher"
                connection-string = "Persist Security Info=False;User ID=""" + sqlserverId + """;Password=""" + sqlserverPwd + """;Initial Catalog=AkkaPersistence;Server=""" + sqlserverIp + """" 
                connection-timeout = 30s
                schema-name = dbo
                table-name = ShardingSnapshotStore
                auto-initialize = on
              }
            }
          } 
          extensions = ["Akka.Cluster.Tools.PublishSubscribe.DistributedPubSubExtensionProvider,Akka.Cluster.Tools"]
        }
        """
    printfn "%s" config
    
    let conf = Configuration.parse config
    if ifClusterInfo then
        conf.WithFallback(ClusterSharding.DefaultConfig())
        //.WithFallback(Akka.Cluster.Tools.Singleton.ClusterSingletonManager.DefaultConfig())            
    else
        conf

let printPeer (peers:System.Collections.Generic.List<Peer>) =
    let ps = peers |> Seq.toArray
    ps
    |> Array.iter (fun peer ->
        printfn "%A" peer.IpAddress
    )

let voteMsg = new ConcurrentDictionary<Address, Seeding>()
let electCnt = new ConcurrentDictionary<Address, Self list>()
let cache = new ConcurrentDictionary<CacheKey, obj>()

let getLeaderBase (transMgr:TransportManager) (cluster: Akka.Cluster.Cluster) bornDt selfClusterId selfIp =
    let sendSeeding (seedingObj:Seeding) = 
        fun (peerAddress:string) ->
            async {
                printfn "\n%s tell %s: %A" selfIp peerAddress seedingObj
            } |> Async.Start
            transMgr.SendAsyncTCP(peerAddress, seedingObj.toMsg) |> Async.AwaitTask
    let asyncBlock (e:PeerChangeEventArgs) = 
        async {
            //let peers:System.Collections.Generic.List<P2PNET.TransportLayer.Peer> = e.Peers
            //let ps = peers |> Seq.toArray
            let ps = e.Peers |> Seq.toArray |> Array.distinct
    
            let! notifyIAmIn =
                ps 
                |> Array.append (if e.removedPeer <> null then [|e.removedPeer|] else [||])
                |> Array.choose (fun peer ->
                    match true with
                    //| _ when peer.IpAddress = selfIp -> 
                    //    printfn "getLeaderBase: selfIp"
                    //    None
                    | _ when peer.IsPeerActive && cluster.State.Leader <> null ->
                        //joined
                        if peer.IpAddress = selfIp then None
                        else
                            Some (
                                let welcome = Welcome (selfClusterId, bornDt, cluster.State.Leader) // .ToString("yyyy-MM-dd HH:mm:ss")
                                sendSeeding welcome peer.IpAddress
                            )
                    | _ when peer.IsPeerActive ->
                        Some (
                            let vote = Recommand (selfClusterId, bornDt, selfIp) // .ToString("yyyy-MM-dd HH:mm:ss")
                            sendSeeding vote peer.IpAddress
                        ) 
                    | _ when not peer.IsPeerActive ->
                        Some (
                            let nvs = NoVotingShare (selfClusterId, DateTime.Now, peer.IpAddress) // .ToString("yyyy-MM-dd HH:mm:ss")
                            sendSeeding nvs peer.IpAddress
                        ) 
                
                )
                |> Async.Parallel
        
            //do! Async.Sleep 10000 //wait 10 seconds to wait for welcome finished
            ()
        }
    asyncBlock, fun leader -> sendSeeding <| Welcome (selfClusterId, bornDt, leader)

let sendRecommandBase (transMgr:TransportManager) (cluster: Akka.Cluster.Cluster) =             
    match voteMsg.IsEmpty with
    | true -> ()
    | false ->
        let (Recommand((_, addr), _, _)) = 
            (voteMsg
            |> Seq.toArray
            |> Array.maxBy (fun r ->
                let (Recommand((id_, addr_), dt_, _)) = r.Value
                id_
            )).Value
        voteMsg.Clear ()
        printfn "transMgr.SendToAllPeersAsyncTCP"
        transMgr.SendToAllPeersAsyncTCP((Elect (addr, cluster.SelfAddress)).toMsg)
        |> Async.AwaitTask |> Async.Start

let locker = ref 0
let locker2 = ref 0
let mutable wfcDisposable : IDisposable = null
let mutable onlyVillage = false

let setWaitCandidateBase (waitForCandidate: Timers.Timer) (sendRecommand:'T0 -> unit) millSec ifOnlyVillage =
    lock locker (fun () ->      
        if not ifOnlyVillage then
            printfn "setWaitCandidateBase: not ifOnlyVillage"
            if not onlyVillage then
                if wfcDisposable <> null then
                    wfcDisposable.Dispose ()
                waitForCandidate.Stop ()
                printfn "reset timer from %f to %f" waitForCandidate.Interval millSec
                if millSec > 0.0 then
                    waitForCandidate.Interval <- millSec
                    wfcDisposable <- 
                        waitForCandidate.Elapsed.Subscribe sendRecommand
                    waitForCandidate.Start ()
                else
                    printfn "invalid millSec: %f" millSec
        else
            printfn "setWaitCandidateBase: ifOnlyVillage"
            onlyVillage <- true
            if wfcDisposable <> null then
                wfcDisposable.Dispose ()
            waitForCandidate.Stop ()
    )

let countElectBase (cluster: Akka.Cluster.Cluster) (setWaitCandidate: float -> bool -> unit) majorityNodeCount selfIp (addr:Address) (from:Self) =
    lock locker2 (fun () ->      
        let selfList = 
            electCnt.AddOrUpdate(
                addr
                , fun _ -> [from]
                , fun _ curVal ->
                    curVal
                    |> List.append [from]
                    |> List.map (fun v -> converter v)
                    |> List.distinct
                    |> List.map (fun v -> recover v)
            )
        printfn "electCnt.Keys.Count: %d, selfList.Length: %d" electCnt.Keys.Count selfList.Length
        if electCnt.Keys.Count = 1 && selfList.Length >= majorityNodeCount && cluster.State.Leader = null then
            printfn "%s first join with election %A" selfIp addr
            //let il = ImmutableList.Create<Address>(seq[addr]|>Seq.toArray)  
            //cluster.JoinSeedNodes il
            let il = 
                //if (converter addr).Host = myIP then
                //    ImmutableList.Create<Address>(seq[addr.changeHost "localhost";]|>Seq.toArray)  
                //else
                    ImmutableList.Create<Address>(seq[addr                       ;]|>Seq.toArray)  
            cluster.JoinSeedNodes il
            setWaitCandidate 0.0 true
    )

let procMsgBase (cluster: Akka.Cluster.Cluster) (setWaitCandidate: float -> bool -> unit) (countElect:Address -> Self -> unit) (sendWelcome: Leader -> string -> Async<_>) (bornDt:DateTime) selfIp (windowSize:float) (s:Seeding) (e:MsgReceivedEventArgs) =
    //let recommandDate = cache.[RecommandDate] :?> DateTime
    match s with
    | Recommand((_, candidateAddr), candidateBorn, candidateIp) ->
        if cluster.State.Leader = null then
            if (candidateBorn <= bornDt.AddMilliseconds windowSize) then
                voteMsg.AddOrUpdate (
                    candidateAddr//.changeHost candidateIp
                    , fun _ -> s
                    , fun _ _ -> s
                ) |> ignore
                let c20 = candidateBorn.AddMilliseconds windowSize
                if cache.[RecommandDate] :?> DateTime > c20 then
                    cache.AddOrUpdate (
                        RecommandDate
                        , fun _ -> box c20
                        , fun _ _ -> box c20
                    ) |> ignore  
                    printfn "candidate recommand earlier than self"
                    setWaitCandidate (float (c20 - System.DateTime.Now).TotalMilliseconds) false
                else
                    printfn "no need to make in advance: %A" bornDt
                    //自己的 recommandDate 比 candidate send 來的大，那要取小
            
            else
                printfn "too late to born: %A" bornDt
                //自己太晚生，現在是推舉階段，停止timer不參與投票
                setWaitCandidate 0.0 true
        else
            printfn "already joined a cluster"

            if e.RemoteIp = selfIp then ()
            else

                sendWelcome cluster.State.Leader e.RemoteIp |> Async.Ignore |> Async.Start
            setWaitCandidate 0.0 true

    | Welcome (thatPeer, thatPeerBorn, thatPeerLeader) ->
        let mutable leaderObj = Unchecked.defaultof<obj>
        match cluster.State.Leader <> null with
        | true -> //cache.TryGetValue(KLeader, ref leaderObj)
            printfn "%A already joined: %A" selfIp thatPeerLeader
        | false when onlyVillage ->
            printfn "%A first join with welcome: %A " selfIp thatPeerLeader
            let il = 
                //if (converter thatPeerLeader).Host = myIP then
                //    ImmutableList.Create<Address>(seq[thatPeerLeader.changeHost "localhost";]|>Seq.toArray)  
                //else
                    ImmutableList.Create<Address>(seq[thatPeerLeader                       ;]|>Seq.toArray)  
            cluster.JoinSeedNodes il
            setWaitCandidate 0.0 true
        | false ->
            printfn "%A first join with welcome: %A " selfIp thatPeerLeader
            let il = 
                //if (converter thatPeerLeader).Host = myIP then
                //    ImmutableList.Create<Address>(seq[thatPeerLeader.changeHost "localhost";]|>Seq.toArray)  
                //else
                    ImmutableList.Create<Address>(seq[thatPeerLeader                       ;]|>Seq.toArray)  
            cluster.JoinSeedNodes il
            setWaitCandidate 0.0 true

    | NoVotingShare (msgFrom, disconnectedTime, host) -> 
        let addr2remove = 
            (voteMsg
            |> Seq.toArray
            |> Array.filter (fun kv ->
                let (Recommand (_, _, h)) = kv.Value
                h = host
            )).[0].Key
        let mutable sc = Unchecked.defaultof<Seeding>
        match voteMsg.TryRemove(addr2remove, ref sc) with
        | true ->
            printfn "%A removed recommand %A" selfIp sc
        | false ->
            printfn "%A removed recommand %A already" selfIp sc

    | Elect (candidate, voter) ->
        countElect candidate voter
 

