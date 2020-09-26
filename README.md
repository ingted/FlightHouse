# FlightHouse

A F# based Akka.Net multiple node seeding tool.

At each node you want to deploy a seed node, just configure the "majoritynodecount" number in app.config, e.g. 3 (of 5 seed nodes).
They will auto elect a leader with P2P favor, and auto vote until the vote of every node received meets the majoritynodecount, all the 5 nodes will invoke joinseednode to form the Akka cluster.

# P2PNET patch

For the Fsi use case, I did some change to P2PNET to make it work fine with Fsi in Windows. 

# Auto recover

Every node crashed would easily rejoin with the welcome message. You can kill each process of each nodes and restart the program to see they found out each other and auto join again.

# Support

anibal.yeh@gmail.com
+886908208029

10:00AM ~ 22:00PM GMT+8

# The output

https://github.com/ingted/FlightHouse/wiki/Four-nodes-demo

