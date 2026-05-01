# These CAN bus components allow the user to setup and configure sending and receiving data across a CAN bus connection.

# can-bus-channel-assignment Component
The channel assignment will be a modal dialog overlay with the following:
- Channel selection using shared channel selector
- Offset: 0-7
- Length: 1-8 bytes
- Mask: hex value default to FF or however long the previous field is indicating. 
- Signed: slider button default to unsigned
- Multipler: double value
- Divider: double value
- Constant: double value

# can-bus-edit-message Component
This allows a user to add or edit a new message. It has fields for:
- ID Type (slider button): standard or extended 11 or 29 bit
- CAN ID (hex): ID with validation based on whether it is 11 or 29 bits with previous field
- DLC: 1-8
- Byte Order: Big Endian/Normal or Little Endian/Word Swap
- RX/TX: slider button defaulted to receive

# can-bus-table Component
This displays graphically the CAN Bus configuration with a table with configuration fields at the top.
These configuration fields include: 
- Interface name: default to "can0" for tab 1 and "can1" for tab 2.
- Bit Rate: 1 Mbs, 800 Kbs, 500 Kbs, 250 Kbs, 125 Kbs, 100 Kbs, 50 Kbs, 25 Kbs, 10 Kbs
- Slient on CAN Bus: Slider toggle button control
- Directly above the table should be a button "Add Message" that put a new message at the bottom of the table.

## CAN Table Columns
- (button controls - no header name): this has two icon buttons for Edit Message and Delete Message
- On/Off: Slider button for enabling or disabling the message
- CAN ID: 11 or 29 but hex ID
- RX/TX: whether the message is being send or is being received
- Byte Order: Big/Normal or Little/Word Swap
- DLC: Data Length 1-8 bytes
- Byte 1: Channel mapping for Byte 1
- Byte 2: Channel mapping for Byte 2
- Byte 3: Channel mapping for Byte 3
- Byte 4: Channel mapping for Byte 4
- Byte 5: Channel mapping for Byte 5
- Byte 6: Channel mapping for Byte 6
- Byte 7: Channel mapping for Byte 7
- Byte 8: Channel mapping for Byte 8

Note that channel mappings can span multiple bytes. It is also not required to have a channel mapping assigned, in which case the data being received or sent is ignored.

The channel mapping cells should be a button that allows the user to edit the channel assignment. The name of the channel is to be shown in the button. If no channel is assigned, it should read "Unassigned"

