# HaierAC
Haier Aircoditioning Logger is added to GitHub for contributors to get to know how the new (?) Haier AC firmware protocol works.

Hardware used:
* Unit: Haier Tundra 2.0 Single-split Airconditioning set - 5,0 kW
* Wifi: Official Haier USB Wi-Fi module (KZW-W002)

## Getting Started

1. When started for the first time, this tool will scan your local network (only last number 1.2.3.x) for devices listening to port 56800.
2. Fill in the correct airco IP and MAC Address (currently not used)
3. Restart tool

It will now begin to log all changes to screen, and Haier.log, and already shows what it understands.

The reason for the tool is getting to know the data structure and protocol so we can control it ourselfves with the official app or remote control, but from Home Assistant.

Most code is in the Program.cs, and the data structure is in `public struct HaierResponse`

## Known Issues

* Network Scanner will only work on Windows, alternatively you can check the IP/mac on the router
* It does NOT control anything
* Many things of the data structure is unknown, but hopefully all what is displayed is correct
* It loses the connection EVERY 15 seconds, looks like it might be some ReceiveTimeout, but cannot get it working, if you can, please help! :) Also might be that we need a keepalive command every 10 seconds or so to let the airco know we are still listening.

If you can help in any way, even just let it run and gather state changes could be of help. This way we can get all the possibilities.
