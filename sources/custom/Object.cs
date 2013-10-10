using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Gst {

	public class PropertyNotFoundException : Exception {}
	
	partial class Object 
	{
		private Dictionary <string, bool> PropertyNameCache = new Dictionary<string, bool> ();

		[DllImport ("libgobject-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr g_object_class_find_property (IntPtr klass, IntPtr name);


		bool PropertyExists (string name) {
			if (PropertyNameCache.ContainsKey (name))
				return PropertyNameCache [name];

			var ptr = g_object_class_find_property (GType.GetClassPtr (), GLib.Marshaller.StringToPtrGStrdup (name));
			var result = ptr != IntPtr.Zero;
			GLib.Marshaller.Free (ptr);

			// just cache the positive results because there might
			// actually be new properties getting installed
			if (result)
				PropertyNameCache [name] = result;

			return result;
		}

		public object this[string property] {
		  get {
				if (PropertyExists (property)) {
					using (GLib.Value v = GetProperty (property)) {
						return v.Val;
					}
				} else
					throw new PropertyNotFoundException ();
		  } set {
				if (PropertyExists (property)) {
					using (GLib.Value v = new GLib.Value (this, property)) {
						v.Val = value;
						SetProperty (property, v);
					}
				} else
					throw new PropertyNotFoundException ();
		  }
		}

		public void Connect (string signal, SignalHandler handler) {
		  DynamicSignal.Connect (this, signal, handler);
		}

		public void Disconnect (string signal, SignalHandler handler) {
		  DynamicSignal.Disconnect (this, signal, handler);
		}

		public void Connect (string signal, Delegate handler) {
		  DynamicSignal.Connect (this, signal, handler);
		}

		public void Disconnect (string signal, Delegate handler) {
		  DynamicSignal.Disconnect (this, signal, handler);
		}

		public object Emit (string signal, params object[] parameters) {
		  return DynamicSignal.Emit (this, signal, parameters);
		}
	}
}