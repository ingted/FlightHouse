# FlightHouse

A F# based Akka.Net multiple node seeding tool.

At each node you want to deploy a seed node, just configure the "majoritynodecount" number in app.config, e.g. 3 (of 5 seed nodes).
They will auto elect a leader with P2P favor, and auto vote until the vote of every node received meets the majoritynodecount, all the 5 nodes will invoke joinseednode to form the Akka cluster.

# P2PNET patch

For the Fsi use case, I did some change to P2PNET to make it work fine with Fsi in Windows. 

# Auto recover

Every node crashed would easily to rejoin with the welcome message. You can kill each process of each nodes and restart the program to see they found out each other and auto join again.

# Support

anibal.yeh@gmail.com
+886908208029

10:00AM ~ 22:00PM GMT+8

# The output

PS C:\Users\anibal> cd "Z:\SharFTrade\AkkaSeed\FAkkaSeed\bin\Debug"
PS Z:\SharFTrade\AkkaSeed\FAkkaSeed\bin\Debug> .\FAkkaSeed.exe
USAGE: FAkkaSeed.exe [--help] [--windowsize <winsz>] [--majoritynodecount <mnc>] [--transportmanagerport <port>]
                     [--seedport <port> <pubSeedPort>] [--seedhostnametype <hostname> <pubHostname>]
                     [--hostname <string>] [--pubhostname <string>] [--persistdb <string>] [--persistdbuid <string>]
                     [--persistdbpwd <string>]

OPTIONS:

    --windowsize <winsz>  specify a initial candidate recommandation waiting time period
    --majoritynodecount <mnc>
                          specify a reasonable initial seednode number, at should be at least more then half of the
                          node number
    --transportmanagerport <port>
                              --seedport <port> <pubSeedPort>
                              --seedhostnametype <hostname> <pubHostname>
                              --hostname <string>       --pubhostname <string>
                              --persistdb <string>      --persistdbuid <string>
                              --persistdbpwd <string>
                              --help                display this list of options.

myIP: 10.28.199.142

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
              public-hostname = "10.28.199.142"
              hostname = "0.0.0.0"
              public-port = 9000
              port = 9000
            }
          }
                      cluster {
                        auto-down-unreachable-after = off
                        roles = ["petabridge.cmd", "ShardNode", "singletonRole"]
                        sharding {
                            role = "ShardNode"
                            # journal-plugin-id = "akka.persistence.journal.inmem"
                            # snapshot-plugin-id = "akka.persistence.snapshot-store.inmem"
                            journal-plugin-id = "akka.persistence.journal.sharding"
                            snapshot-plugin-id = "akka.persistence.snapshot-store.sharding"
                        }
                        #seed-nodes = [ "akka.tcp://cluster-system@localhost:8081", "akka.tcp://cluster-system@localhost:8082"  ]
                      }
          persistence{
            query.journal.sql {
              max-buffer-size = 10000
            }
            journal {
              plugin = "akka.persistence.journal.sql-server"
              sql-server {
                class = "Akka.Persistence.SqlServer.Journal.BatchingSqlServerJournal, Akka.Persistence.SqlServer"
                connection-string = "Persist Security Info=False;User ID=sa;Password=/'],lp123/'],lp123;Initial Catalog=AkkaPersistence;Server=10.28.112.93"
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
                connection-string = "Persist Security Info=False;User ID=sa;Password=/'],lp123/'],lp123;Initial Catalog=AkkaPersistence;Server=10.28.112.93"
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
                connection-string = "Persist Security Info=False;User ID=sa;Password=/'],lp123/'],lp123;Initial Catalog=AkkaPersistence;Server=10.28.112.93"
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
                connection-string = "Persist Security Info=False;User ID=sa;Password=/'],lp123/'],lp123;Initial Catalog=AkkaPersistence;Server=10.28.112.93"
                connection-timeout = 30s
                schema-name = dbo
                table-name = ShardingSnapshotStore
                auto-initialize = on
              }
            }
          }
          extensions = ["Akka.Cluster.Tools.PublishSubscribe.DistributedPubSubExtensionProvider,Akka.Cluster.Tools"]
        }

[DEBUG][9/26/2020 1:31:28 PM][Thread 0001][EventStream] StandardOutLogger started
[INFO][9/26/2020 1:31:28 PM][Thread 0008][akka://cluster-system/system/log1-NLogLogger] NLogLogger started
[DEBUG][9/26/2020 1:31:28 PM][Thread 0001][EventStream(cluster-system)] Logger log1-NLogLogger [NLogLogger] started
[DEBUG][9/26/2020 1:31:28 PM][Thread 0001][EventStream(cluster-system)] StandardOutLogger being removed
9/26/2020 9:31:43 PM
9/26/2020 9:31:28 PM
setWaitCandidateBase: not ifOnlyVillage
reset timer from 15000.000000 to 14974.001500
<null>
seq []
seq []


10.28.199.142



msg from msg from 10.28.199.142 tell 10.28.199.14210.28.199.14210.28.199.14210.28.199.142 tell 10.28.199.142: Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")10.28.199.14210.28.112.11210.28.112.112 tell  tell  tell  tell
10.28.199.14210.28.199.142: Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")
: Welcome
  (("d41dadba-9521-4a85-9a98-e9f68ddc6135",
    akka.tcp://cluster-system@10.28.112.112:9000), 9/26/2020 6:00:39 PM,
   akka.tcp://cluster-system@10.28.112.112:9000): Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")10.28.112.11210.28.199.142: Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")
: :

: Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")
Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")
Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")
no need to make in advance: 9/26/2020 9:31:28 PM
"10.28.199.142" first join with welcome: akka.tcp://cluster-system@10.28.112.112:9000
msg from 10.28.199.142: Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")
no need to make in advance: 9/26/2020 9:31:28 PM
msg from 10.28.199.142: Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")
setWaitCandidateBase: ifOnlyVillage
no need to make in advance: 9/26/2020 9:31:28 PM
msg from 10.28.199.142: Recommand
  (("fe8dc719-d72b-4674-bfc5-2d5fa5877c5b",
    akka.tcp://cluster-system@10.28.199.142:9000), 9/26/2020 9:31:28 PM,
   "10.28.199.142")
no need to make in advance: 9/26/2020 9:31:28 PM
msg from 10.28.112.112: Welcome
  (("d41dadba-9521-4a85-9a98-e9f68ddc6135",
    akka.tcp://cluster-system@10.28.112.112:9000), 9/26/2020 6:00:39 PM,
   akka.tcp://cluster-system@10.28.112.112:9000)
"10.28.199.142" first join with welcome: akka.tcp://cluster-system@10.28.112.112:9000
setWaitCandidateBase: ifOnlyVillage
<null>
seq []
seq []
<null>
seq []
seq []
akka.tcp://cluster-system@10.28.112.112:9000
seq
  [Member(address = akka.tcp://cluster-system@10.28.112.112:9000, Uid=1992894030 status = Up, role=[ShardNode,singletonRole,petabridge.cmd], upNumber=3);
   Member(address = akka.tcp://cluster-system@10.28.199.142:9000, Uid=844398658 status = Up, role=[ShardNode,singletonRole,petabridge.cmd], upNumber=4)]
seq []
akka.tcp://cluster-system@10.28.112.112:9000
seq
  [Member(address = akka.tcp://cluster-system@10.28.112.112:9000, Uid=1992894030 status = Up, role=[ShardNode,singletonRole,petabridge.cmd], upNumber=3);
   Member(address = akka.tcp://cluster-system@10.28.199.142:9000, Uid=844398658 status = Up, role=[ShardNode,singletonRole,petabridge.cmd], upNumber=4)]
seq []
