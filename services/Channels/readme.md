# Channels
A channel represents a piece of data, such as a sensor temperature, button state, status variable, like gear position, race position, etc. This contains metadata such as base units and display units. Channel data can orginate at the car or from the cloud.

This library defines a channel system that allows for the creation and management of channels. It is intended to be shared acorss the car and cloud, allowing for a consistent way to define and use channels.

Channels can be user defined or "reserved."

## Reserved Channels
Reserved channels are ones known by the application and can be shared across cars and cloud. Part of the intent is to allow for streamlined usage such as in dashboard and alarms. This avoids one-off definitions per car. It can also enable special functions when available, like fuel range analysis when fuel usage channel is available.

### Channel Definition
The channel definition contains metadata about the channel, such as name, description, base units, display units, and other information. This is used to define the channel and its properties.

### Channel Value
The channel value contains the actual data for the channel, such as the current temperature, button state, etc. This is updated in real-time and can be used for various purposes, such as displaying on a dashboard or triggering alarms.


## Math
Math expressions can be used to perform calculations on channel values. This allows for the creation of derived channels that can provide additional insights or information based on existing channels. For example, a derived channel could calculate the average speed based on the speed channel, or calculate fuel efficiency based on fuel usage and distance traveled.

The following maths operators can be used in the maths expressions:
- '+' Addition
- '–' Subtraction
- '*' Multiplication
- '/' Division
 
 #### Relational Operators
 
- '<', LT, lt Less than 
- '<=', LTE, lte Less than or equal to 
- '>', GT, gt Greater than 
- '>=', GTE, gte Greater than or equal to 
- '==', EQ, eq Equal to 
- '!=', NEQ, neq Not equal to 


The following functions can be used in the maths expressions:

- Abs(x)
- Int(x)
- Max(x, y)
- Min(x, y)
- Power(x, y)
- Remainder(x, y)
- Round(x)
- RoundDown(x)
- RoundUp(x)
- sqrt(x)


## Timer
The timer system is a general purpose system for generating incrementing or decrementing time values.

The time value may be fed to a channel which may then be displayed, or used for any other purpose.

### Start and Stop Conditions
The timer starts when the Start condition becomes true and stops when the Stop condition becomes true.

Both the Start and Stop conditions are "edge sensitive". This means that the Timer starts when the Start condition changes from false to true, irrespective of whether the Stop condition is true or false. Similarly, the Timer stops when the Stop condition changes from false to true, irrespective of whether the Start condition is true or false.

For a timer to start, the condition must first be evaluated as false before it can be evaluated as changing to true. To ensure this happens at power-on, the initial condition of both the Start and Stop conditions is true.

Start/Stop conditions can be anything that becomes true (e.g. Battery Voltage greater than 0.00V).

Maximum Value
The maximum timer value is 42,949,672 with decimal places inserted.

For example, if the timer channel has a resolution of 0.1 sec then the maximum value is 4,294,967.2 seconds (49.7 days).

### Limit
Specifies the High Limit value (the Low Limit value is fixed at zero).

When the Count up option is selected and the Roll over when limit exceeded check box is selected, the timer will return to the Low Limit value (zero).

When the Count up option is selected and the Roll over when limit exceeded check box is cleared, the timer will stop at the High Limit value.

When the Count down option is selected and the Roll over when limit exceeded check box is selected, the timer will return to the High Limit value.

When the Count down option is selected and the Roll over when limit exceeded check box is cleared, the timer will stop at the Low Limit value (zero).

Note: The Timer is Analogue or Floating Point, so if the "Exceeded Limit" is "10", for example, it exceeds "10" at "10.00000001" and rolls over or stops immediately.

### Start Setting
When the Enable start setting check box is selected, the timer channel is set to the Start value whenever the Start condition becomes true. On start the timer will immediately begin timing from the start value.

When the Enable start setting check box is cleared the timer will start from its last value, or zero at power up.

### Stop Setting
When the Enable stop setting check box is selected the Timer channel is set to the Stop value whenever the Stop condition becomes true. On stop the timer will remain at this value until it is next started.

When the Enable stop setting check box is cleared the timer will remain at its last value.


## Tables
The general purpose tables allow one or two channels to be translated to a new value via a 2D or 3D table. The new value may feed any channel.

For example a 3D table could be used to control a thermatic fan at various combinations of Engine Temperature and Ground Speed.

Interpolation Types supported are:
- Linear
- CubicSpline
- Polynomial

### 2D Tables
The 2D tables take a single channel value and translates it to another value by way of a 2D lookup table.

The maximum number of points in a 2D table is 20.

The maximum number of 2D tables is 16 (with Advanced Functions enabled) or 4 as standard.

### 3D Tables
The 3D tables take two channel values and translates them to another value by way of a 3D lookup table.

The maximum dimensions of the 3D table is 20 x 20.

The maximum number of 3D tables is 16 (with Advanced Functions enabled) or 4 as standard.


## Logic
### Conditions
Conditions are used to define a true/false condition which may then be used to activate features such as when to start logging, or when to reset the lap count.

A condition is made up of one or more comparisons. A comparison compares a channel to a fixed value or another channel, and requires that the comparison be true for a defined amount of time.

#### Comparisons
- To define a comparison
- Select a channel 
- Select the comparison type 
- Select a value or another channel to compare with   
- Set the modify parameters 
- Comparison Types
#### Magnitude Comparisons
- Greater than or equal to
- Less than or equal to
- Equal to
- Greater than
- Less than

#### Logical Comparisons
- True - the condition is true if the channel value is non zero.
- False - the condition is true if the channel value is zero.
- Updated - the condition is true for a short time (approx. 30 msec) after the channel value is updated. This can be used with channels that are updated infrequently such as Lap Time.
- Bit And - performs a bitwise AND, if the result is non zero then the condition is true.
- Changed By - defines how much the value should change for the condition to become true  

### Reverse Result
Reverses the result of the comparison, i.e. a true condition will be considered false. This feature is only needed in very special circumstances and should normally be left unchecked.


## Counters
Counters are general purpose system items whose value can be incremented or decremented based on the selected channel conditions.


