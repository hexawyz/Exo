Trying to find more informations on Logitech receivers using register B5 (Pairing and Non Volatile Information)

## Receiver C547 here:

B5 00 => Error
B5 01 => Error
B5 02 => 02 040200098017110097aa22b9000000 (Other Receiver Info ?)
B5 03 => 03 3c96bd85010207ff00000000000000 (Receiver Info)
B5 04 ~ B5 19 => Error
B5 2x => Paring Info
B5 3x => Extended Pairing Info
B5 4x => Device Name

Basically, all other parameters returned an error.

The only new information is parameter 02.

Trying to query register B3 (device activity):

Got different results every time (does not take any parameter)
This looks like a counter of some sort. (But on the receiver itself, not the mouse)

00000000000000000000000000000000
0d000000000000000000000000000000
d6000000000000000000000000000000
40000000000000000000000000000000

Trying to query register 80 (Receiver mode) => Error
Trying to query register 82 (Connect device) => Error
Trying to query register B0 (Link quality information) => Error

Trying to query register F1 (Firmware Version):

With parameters 00 00 00 => Invalid Value
With parameters 01 00 00 => 01 04 02
With parameters 02 00 00 => 02 00 09
With parameters 03 00 00 => 03 f9 ed
With parameters 04 00 00 => 04 00 05
With parameters 05 00 00 => 05 f9 20
With parameters 06 00 00 => Invalid Value

## Logitech Unifying Receiver C52B

B5 02 => 02 241100368808040100000000000000
B5 03 => 03 3638ecb40e06080000000000000000
B5 7C => 7c 00220514a7eb027211000000000000

## Logitech Bolt Receiver C548

B5 02 => 02 0501001001b6100600000000000000
B5 03 => 03 3638ecb40e06080000000000000000
B5 7C => 7c 00220514a7eb027211000000000000

## Comparing responses of different receivers

C547 | Lightspeed | B5 02 | 02 040200098017110097aa22b9000000
C52B | Unifying   | B5 02 | 02 241100368808040100000000000000
C548 | Bolt       | B5 02 | 02 0501001001b6100600000000000000
