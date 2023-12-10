using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Exo.Service;

// The event queue will take in any event in form of a GUID, and provide events sequentially.
public class EventQueue
{
	private static readonly UnboundedChannelOptions ChannelOptions = new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false };

	private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(ChannelOptions);

	//public void RegisterEvent(string name) where TEvent : Delegate { }

	//public void GetEvent();
}
