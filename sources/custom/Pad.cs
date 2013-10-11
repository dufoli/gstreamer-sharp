namespace Gst {
	using System;
	using System.Runtime.InteropServices;

	partial class Pad 
	{
		[GLib.Property ("caps")]
		public Gst.Caps Caps {
			get {
				GLib.Value val = GetProperty ("caps");
				Gst.Caps ret = new Gst.Caps ((IntPtr)val);
				val.Dispose ();
				return ret;
			}
		}
	}
}