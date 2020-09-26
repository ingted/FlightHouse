module FAkkaTrade.Types

open System.Text
open P2PNET.TransportLayer
open Akka.Actor
open System
open MBrace.FsPickler
open Argu

type CacheKey =
| RecommandDate
| KLeader

type HeartBeatManager(mHeartBeatMsg: string, mTransMgr: TransportManager) =
    member this.heartBeatMsg = mHeartBeatMsg
    member this.transMgr = mTransMgr
    member val hrtBtTimer = new System.Timers.Timer() with get, set
    member this.StartBroadcasting() =
        let hbSubDisposable =
            this.hrtBtTimer.Elapsed.Subscribe(fun e ->
                let msgBin : byte array = Encoding.ASCII.GetBytes(this.heartBeatMsg)
                let t = this.transMgr.SendBroadcastAsyncUDP(msgBin)
                //printfn "sent heartbeat"
                ()
            )
        this.hrtBtTimer.Interval <- 1000.0
        this.hrtBtTimer.Start()

type PickleAddress = {
    Protocol: string
    System: string
    Host: string
    Port: int option
}
let recover = 
    fun (paddr:PickleAddress) ->
        let (Some port) = paddr.Port
        Address.Parse (sprintf "%s://%s@%s:%d" paddr.Protocol paddr.System paddr.Host port)
let converter = 
    fun (addr:Address) ->
        {
            Protocol = addr.Protocol
            System = addr.System
            Host = addr.Host
            Port = match addr.Port.HasValue with | true -> Some addr.Port.Value | false -> None
        }
type Address with
    member this.changeHost host =
        { converter this with
            Host = host }
        |> recover
        //this

type Self = Address
type PSelf = PickleAddress
type ClusterId = string * Self
type PClusterId = string * PSelf
type Leader = Address
type PLeader = PickleAddress
type Host = string
type PSeeding =
| PWelcome of (PClusterId * DateTime * PLeader)
| PRecommand of (PClusterId * DateTime * Host)
| PElect of (PickleAddress * PSelf)
| PNoVotingShare of (PClusterId * DateTime * Host)
type Seeding =
| Welcome of (ClusterId * DateTime * Leader)
| Recommand of (ClusterId * DateTime * Host)
| Elect of (Address * Self)
| NoVotingShare of (ClusterId * DateTime * Host)
    with 
        member this.toMsg =
            let paddr = 
                match this with
                | Welcome ((s, a), dt, l) -> 
                    PWelcome ((s, converter a), dt, converter l)
                | Recommand ((s, a), dt, h) ->
                    PRecommand ((s, converter a), dt, h)
                | NoVotingShare ((s, a), dt, h) ->
                    PNoVotingShare ((s, converter a), dt, h)
                | Elect (a, s) ->
                    PElect (converter a, converter s)
            let binarySerializer = FsPickler.CreateBinarySerializer()
            binarySerializer.Pickle paddr
        static member fromMsg msg =
            try 
                let binarySerializer = FsPickler.CreateBinarySerializer()            
                match binarySerializer.UnPickle<PSeeding> msg with
                | PWelcome ((s, a), dt, l) -> 
                    Welcome ((s, recover a), dt, recover l)
                | PRecommand ((s, a), dt, h) ->
                    Recommand ((s, recover a), dt, h)
                | PNoVotingShare ((s, a), dt, h) ->
                    NoVotingShare ((s, recover a), dt, h)
                | PElect (a, s) ->
                    Elect (recover a, recover s)
                |> Some
            with
            | exn -> 
                None
        member this.dt =
            match this with
            | Welcome (_, dt, _) -> dt
            | Recommand (_, dt, _) -> dt
            | NoVotingShare (_, dt, _) -> dt


type HostNameType =
| Auto
| Customize


type Arguments =
    | WindowSize of winsz : float
    | MajorityNodeCount of mnc : int
    | TransportManagerPort of port : int
    | SeedPort of port : int * pubSeedPort : int
    | SeedHostnameType of hostname : HostNameType * pubHostname : HostNameType
    | HostName of string
    | PubHostName of string
    | PersistDB of string
    | PersistDBUID of string
    | PersistDBPWD of string
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | WindowSize _ -> "specify a initial candidate recommandation waiting time period"
            | MajorityNodeCount _ -> "specify a reasonable initial seednode number, at should be at least more then half of the node number"
            | SeedPort _ -> ""
            | SeedHostnameType _ -> ""
            | _ -> ""

let parser = ArgumentParser.Create<Arguments>(programName = "FAkkaSeed.exe")

printfn "%s" <| parser.PrintUsage()
