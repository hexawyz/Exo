using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace DeviceTools
{
	public class ConfigurationManagerException : Exception
	{
		internal ConfigurationManagerException(NativeMethods.ConfigurationManagerResult resultCode) : this((uint)resultCode)
		{
		}

		public ConfigurationManagerException(uint resultCode)
			: this(resultCode, $"Configuration manager returned the following error: {((NativeMethods.ConfigurationManagerResult)resultCode).ToString()}.")
		{
		}

		public ConfigurationManagerException(uint resultCode, string message) : base(message)
		{
			ResultCode = resultCode;
		}

		public ConfigurationManagerException(uint resultCode, string message, Exception innerException) : base(message, innerException)
		{
			ResultCode = resultCode;
		}

		protected ConfigurationManagerException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
			ResultCode = info.GetUInt32(nameof(ResultCode));
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue(nameof(ResultCode), ResultCode);
			base.GetObjectData(info, context);
		}

		public uint ResultCode { get; }
	}
}
