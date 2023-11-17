# TV Sound Controller

The app that allows automatic TV volume adjustment if it is too high or too low.
Supports only Samsung TVs released after 2016.
  
## Usage

Acquire a unidirectional microphone with short input range (like, Røde M1). Install the mic very close to the TV speaker.

Learn the TV's IP address (in the form of `XXX.XXX.XXX.XXX`) and MAC address (in the form of `XX:XX:XX:XX:XX:XX`; note that aslo `-` may be displayed instead of `:`).
To discover these addressed, you can either
1. check it from Network Settings or Network Connection of the TV,
2. open the webpage of your home router (e.g. 192.168.1.1) and check the list of connected devices somewhere in the Wireless settings, although it may be hard to figure out which of the listed devices is the TV you want to control.


Start this app and enter these addresses on the start screen. If the addresses are correct, the TV will be connected (and turned on, if needed) upon you press the `Connect` button. Note that sometimes the app fails to connected to the TV even if the addresses are correct.

Once the TV is connected, set the current TV volume (`Initial volume`) and maximum allowed volume (`Maximum volume`). Then play some quiet music on the TV and try out various `Scale` value, until the the mic level visible in the app interface becomes somewhat low, though its bar remains clearly visible.

Finally, adjust `Minimum level` and `Maximum level` so that too quiet sounds drop the mic level below with lower bar, and too loud sounds raise the mi level above the higher level.

Click `Start` button and enjoy automatic sound countrol!

## Notes

Do not connect to a TV within 30 seconds after it was turned off.

## References

The Samsing TV communication code is based on 
- [luvaihassanali/SamsungRemote](https://github.com/luvaihassanali/SamsungRemote/)

Other references:
- [Toxblh/samsung-tv-control](https://github.com/Toxblh/samsung-tv-control)
- [Bntdumas/SamsungIPRemote](https://github.com/Bntdumas/SamsungIPRemote) 
- [jakubpas/samsungctl](https://github.com/jakubpas/samsungctl)
- [balmli/com.samsung.smart](https://github.com/balmli/com.samsung.smart)
