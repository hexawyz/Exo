# The device

Viewsonic VP85-4K is a regular Windows monitor.
It has various settings that can be adjusted, one of which being the sound volume for its jack output.

# Analysis

This one seems pretty simple, fortunately.
Being mainly interested in brightness and audio control, for which the official application sucks quite a bit (It is **slooooow**), it is a relief to know that this monitor seems to operate on a pretty standard way.

A tool such as ClickMonitorDDC shows that most of its features (even some possibly not displayed in the official tool) seem to be easily accessible by issuing DDC-CI commands.

ClickMonitorDDC's UI is actually pretty good, but a bit too technical (it is a technical tool !), so I don't see myself running this in the background, but it does a good job of exploring the available commands and quiclky playing with them.

## VCP codes returned by ClickMonitorDDC / Reverse engineering some codes

````
(prot(monitor)type(LCD)model(VP2785 series)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 0B 0C 10 12 14(01 02 04 05 06 08 0B 0E 0F 10 11 12 13 15 16 17 18) 16 18 1A 1D(F1 15 0F 11 12 17) 21(01 02 03 04 05) 23(01 02 03) 25(01 02 03) 27(01 02) 2B(01 02) 2D(01 02) 2F(01 02) 31(01 02) 33(01 02) 52 59 5A 5B 5C 5D 5E 60(15 0F 11 12 17) 62 66(01 02) 67(00 01 02 03) 68(01 02 03 04) 6C 6E 70 72(00 78 FB 50 64 78 8C A0) 87 8D 96 97 9B 9C 9D 9E 9F A0 AA AC AE B6 C0 C6 C8 C9 CA(01 02 03 04 05) CC(01 02 03 04 05 06 07 09 0A 0B 0C 0D 12 16) D6(01 04 05) DA(00 02) DB(01 02 03 05 06) DC(00 03 04 30 31 32 33 34 35 36 37 38 39 3A 3B 3C 3D 3E) DF E1(00 19 32 4B 64) E2 E3(00 01 02) E4(01 02) E5(01 02) E7(01 02) E8(01 02 03 04 05) E9(01 02) EA EB EC ED(01 02) EF(01 02) F3(00 01 02 03))mswhql(1)asset_eep(40)mccs_ver(2.2))
````

Most info should be included in this capability string, and ClickMonitorDDC shows a table with the available codes that can't be exported.

Instead, I'll list interesting codes one by one taking info from the app and trying a few features of the monitor.
Reverse engineering most codes is easy when you can compare your settings with the value displayed.

Code | Official Description   | RE Description          | Values

10   | Brightness             | Brightness              | 0 to 100
1D   |                        | PIP Source ?            | 241 21 15 17 18 23 (Value changed from 0 to 241 when enabling PIP… But changing the value manually seems to do nothing)
23   |                        | Low Input Lag           | 1 2 3 (Off, Advanced, Ultra Fast)
25   |                        | Response Time           | 1 2 3 (Off, Advanced, Ultra Fast)
62   | Audio speaker volume   | Audio speaker volume    | 0 to 100
96   | Window Position (TL_Y) | PIP Window Position X:Y | 0 to 25700 => Actually to combined bytes from hex 00 to 64
97   | Window Position (BR_X) | PIP Window Size         | 0 to 10 (Smallest to largest size; largest is about 1/9 of the screen)
E1   |                        | Advanced DCR            | 0 25 50 75 100
E2   |                        | Blue light Filter       | 0 to 100
E8   |                        | Multi Picture mode      | 1 2 3 4 5 (Disabled; PIP; Left/Right; Top/Bottom; Quad) (ClickMonitorDDC has trouble reading back the value, maybe because of screen switching modes)


Source IDs <=> Name mapping
241 DisplayPort ?
21
15
17
18
23 USB-C

