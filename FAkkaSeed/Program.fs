#if INTERACTIVE
#load "types.fs"
#load "functions.fs"
#endif

open FAkkaTrade.Types
open FAkkaTrade.Functions
open P2PNET.TransportLayer
open Akka
open Akka.Actor
open Akka.Cluster
open System.Text
open Akkling
open Akkling.Cluster.Sharding
open Akka.Cluster.Sharding
open System.Collections.Concurrent
open System.Net
open MBrace.FsPickler
open System
open System.Collections.Immutable
open System.Reflection
open MBrace.FsPickler.Combinators
open System.Threading.Tasks
open Petabridge.Cmd.Host
open Petabridge.Cmd.Cluster
open Argu
open Petabridge.Cmd.Cluster.Sharding


[<EntryPoint>]
let main argv =
    try
        let results = parser.Parse argv
        let portNum = results.GetResult TransportManagerPort
        //let results = parser.Parse [| "--seedhostnametype"; "auto"; "auto"|]
        let system_local_seed_id = System.Guid.NewGuid().ToString()
        let myIP = getMyIp ()
        let (hn, pbhn) = results.GetResult SeedHostnameType
        let hnm, phn = 
            match (hn, pbhn) with
            | (Auto, Auto) -> myIP, myIP
            | (Customize, Auto) -> results.GetResult HostName, myIP
            | (Auto, Customize) -> myIP, results.GetResult PubHostName
            | (Customize, Customize) -> results.GetResult HostName, results.GetResult PubHostName
           
        let seedPort, pubSeedPort = results.GetResult SeedPort
        let sqlhost, sqluser, sqlpwd = 
            (results.GetResult PersistDB), 
            (results.GetResult PersistDBUID),
            (results.GetResult PersistDBPWD)

        //let majorityNodeCount = 1
        //let windowSize = 30000.0
        let startNode windowSize majorityNodeCount =            
            let transMgr : TransportManager = new TransportManager(portNum, true)
            transMgr.StartAsync() |> Async.AwaitTask |> Async.Start
            //Async.RunSynchronously(TransportManager.GetLocalIPAddress2() |> Async.AwaitTask, 100)           

            //let myIP = 
            //    try
            //        Async.RunSynchronously(transMgr.GetIpAddress() |> Async.AwaitTask, 100)
            //    with
            //    | :? TimeoutException as texn ->
            //        printfn "%%%%%%%%%%%%%%%% timeout %%%%%%%%%%%%%%%%"
            //        Async.RunSynchronously(transMgr.GetIpAddress() |> Async.AwaitTask, 100)
            //    | _ ->
            //        printfn "%%%%%%%%%%%%%%%% getIp Error %%%%%%%%%%%%%%%%"
            //        reraise ()
            printfn "myIP: %s" myIP
            
            let system_local = 
                Akka.Actor.ActorSystem.Create(
                    "cluster-system", 
                        configWithPort pubSeedPort seedPort sqlhost sqluser sqlpwd ["ShardNode"; "singletonRole"] hnm phn true) 
            //let system_local = Akka.Actor.ActorSystem.Create("cluster-system", configWithPort 9010 "10.28.112.93" "sa" "/'],lp123/'],lp123" ["ShardNode"; "singletonRole"] "10.28.199.142" false) 

            let cmd = PetabridgeCmd.Get(system_local)
            cmd.RegisterCommandPalette(ClusterCommands.Instance) |> ignore
            cmd.RegisterCommandPalette(ClusterShardingCommands.Instance) |> ignore
            cmd.Start()

            let cluster = Cluster.Get system_local
        
            let waitForCandidate = new Timers.Timer(windowSize)        
            waitForCandidate.AutoReset <- false

            let dt = DateTime.Now
            let _ = cache.TryAdd (RecommandDate, dt)
            let hrtBtMgr : HeartBeatManager = new HeartBeatManager("heartbeat", transMgr)
    
            let me = system_local_seed_id, cluster.SelfAddress //.changeHost myIP
            let getLeader, sendSeeding = getLeaderBase transMgr cluster dt me myIP
            let sendRecommand = 
                fun (e:Timers.ElapsedEventArgs) ->
                    sendRecommandBase transMgr cluster
            let setWaitCandidate = setWaitCandidateBase waitForCandidate sendRecommand

            let countElect = countElectBase cluster setWaitCandidate majorityNodeCount myIP

            let procMsg = procMsgBase cluster setWaitCandidate countElect sendSeeding dt myIP windowSize

            let msgRcvDisposable = 
                transMgr.MsgReceived.Subscribe (fun e ->
                    match Seeding.fromMsg e.Message with
                    | Some msg ->
                        printfn "msg from %s: %A" e.RemoteIp msg
                        procMsg msg e
                    | None ->
                        //printfn "msg not seeding from %s" e.RemoteIp
                        ()
                )
            let peerChgDisposable = 
                transMgr.PeerChange.Subscribe(fun e ->
                    //printPeer e.Peers
                    getLeader e |> Async.Start
                )
            let transMgrTask = transMgr.StartAsync()
            printfn "%A" <| dt.AddMilliseconds(windowSize)
            printfn "%A" System.DateTime.Now
            setWaitCandidate (float (dt.AddMilliseconds(windowSize) - System.DateTime.Now).TotalMilliseconds) false
            waitForCandidate.Start()
            hrtBtMgr.StartBroadcasting()
            //let _ = System.Console.ReadLine ()
            let rec show () =
                async {
                    printfn "%A" cluster.State.Leader
                    printfn "%A" cluster.State.Members
                    printfn "%A" cluster.State.Unreachable
                    do! Async.Sleep 5000
                    do! show ()
                }
            show () |> Async.Start
            //let _ = System.Console.ReadLine ()
            Task.WaitAll [| system_local.WhenTerminated |]
        startNode (results.GetResult WindowSize) (results.GetResult MajorityNodeCount)
        0
    with
    | exn ->
        printfn "%A" exn
        -1 // return an integer exit code
