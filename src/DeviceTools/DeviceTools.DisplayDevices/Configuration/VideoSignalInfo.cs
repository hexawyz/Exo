using System.Drawing;

namespace DeviceTools.DisplayDevices.Configuration
{
	public readonly struct VideoSignalInfo
	{
		private readonly NativeMethods.DisplayConfigVideoSignalInfo _videoSignalInfo;

		internal VideoSignalInfo(NativeMethods.DisplayConfigVideoSignalInfo videoSignalInfo)
		{
			_videoSignalInfo = videoSignalInfo;
		}

		public long PixelClockRate => (long)_videoSignalInfo.PixelRate;
		public Rational HorizontalSyncFrequency => _videoSignalInfo.HorizontalSyncFreq;
		public Rational VerticalSyncFrequency => _videoSignalInfo.VerticalSyncFreq;
		public Size ActualSize => new Size((int)_videoSignalInfo.ActiveSize.Cx, (int)_videoSignalInfo.ActiveSize.Cy);
		public Size TotalSize => new Size((int)_videoSignalInfo.TotalSize.Cx, (int)_videoSignalInfo.TotalSize.Cy);
		public VideoSignalStandard VideoStandard => _videoSignalInfo.VideoStandard;
		public int VerticalSyncFrequencyDivider => _videoSignalInfo.VerticalSyncFreqDivider;
		public ScanlineOrdering ScanlineOrdering => _videoSignalInfo.ScanLineOrdering;
	}
}
