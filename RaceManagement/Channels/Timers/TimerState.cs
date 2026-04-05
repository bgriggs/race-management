using System;
using System.Collections.Generic;
using System.Text;

namespace Channels.Timers;

public class TimerState
{
    public int Id { get; set; }
    public DateTime? Started { get; set; }
    public int StartValue { get; set; }
}
