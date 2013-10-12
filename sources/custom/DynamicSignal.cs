//
//
// Copyright (C) 2006 Novell Inc.
// Copyright (C) 2009 Sebastian Dröge <sebastian.droege@collabora.co.uk>
// Copyright (C) 2013 Stephan Sundermann <stephansundermann@gmail.com>
//
// This class implements functions to bind callbacks to GObject signals
// dynamically and to emit signals dynamically.
//
//

using GLib;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections;

namespace Gst {

  delegate void GClosureMarshal (IntPtr closure, ref GLib.Value retval, uint argc, IntPtr argsPtr,
                                 IntPtr invocation_hint, IntPtr data);

  public delegate void SignalHandler (object o, SignalArgs args);

  public static class DynamicSignal {

    private static readonly int gvalue_struct_size = Marshal.SizeOf (typeof (GLib.Value));

    class ObjectSignalKey {
      object o;
      string signal_name;

      public ObjectSignalKey (object o, string name) {
        this.o = o;
        signal_name = name;
      }

      public override bool Equals (object o) {
        if (o is ObjectSignalKey) {
          ObjectSignalKey k = (ObjectSignalKey) o;
          return k.o.Equals (this.o) && signal_name.Equals (k.signal_name);
        }
        return base.Equals (o);
      }

      public override int GetHashCode() {
        return o.GetHashCode() ^ signal_name.GetHashCode();
      }
    }

    class SignalInfo {
      uint handlerId;
      IntPtr closure;
      Delegate registeredHandler;
      Type argsType;

      public IntPtr Closure {
        get {
          return closure;
        } set {
          closure = value;
        }
      }

      public uint HandlerId {
        get {
          return handlerId;
        } set {
          handlerId = value;
        }
      }

      public Delegate RegisteredHandler {
        get {
          return registeredHandler;
        } set {
          registeredHandler = value;
        }
      }

      public Type ArgsType {
        get {
          return argsType;
        } set {
          argsType = value;
        }
      }

      public SignalInfo (uint handlerId, IntPtr closure, Delegate registeredHandler) {
        this.handlerId = handlerId;
        this.closure = closure;
        this.registeredHandler = registeredHandler;

        if (!IsValidDelegate (registeredHandler))
          throw new Exception ("Invalid delegate");

        MethodInfo mi = registeredHandler.Method;
        ParameterInfo[] parms = mi.GetParameters ();
        this.argsType = parms[1].ParameterType;
      }

      public void UpdateArgsType (Delegate d) {
        if (!IsCompatibleDelegate (d))
          throw new Exception ("Incompatible delegate");

        MethodInfo mi = d.Method;
        ParameterInfo[] parms = mi.GetParameters ();

        Type t1 = parms[1].ParameterType;
        Type t2 = argsType;

        if (t1 == t2)
          return;

        if (t1.IsSubclassOf (t2))
          argsType = t1;
        else if (t2.IsSubclassOf (t1))
          argsType = t2;
        else
          throw new Exception ("Incompatible delegate");
      }

      public bool IsCompatibleDelegate (Delegate d) {
        if (!IsValidDelegate (d))
          return false;

        MethodInfo mi = d.Method;
        ParameterInfo[] parms = mi.GetParameters ();

        if (parms[1].ParameterType != this.argsType &&
            !parms[1].ParameterType.IsSubclassOf (this.argsType) &&
            !this.argsType.IsSubclassOf (parms[1].ParameterType))
          return false;

        return true;
      }

      public static bool IsValidDelegate (Delegate d) {
        MethodInfo mi = d.Method;

        if (mi.ReturnType != typeof (void))
          return false;

        ParameterInfo[] parms = mi.GetParameters ();
        if (parms.Length != 2)
          return false;

        if (parms[1].ParameterType != typeof (GLib.SignalArgs) &&
            !parms[1].ParameterType.IsSubclassOf (typeof (GLib.SignalArgs)))
          return false;

        return true;
      }
    }

    static Hashtable SignalHandlers = new Hashtable();

    static GClosureMarshal marshalHandler = new GClosureMarshal (OnMarshal);

    public static void Connect (GLib.Object o, string name, SignalHandler handler) {
      Connect (o, name, false, (Delegate) handler);
    }

    public static void Connect (GLib.Object o, string name,
                                bool after, SignalHandler handler) {
      Connect (o, name, after, (Delegate) handler);
    }

    public static void Connect (GLib.Object o, string name, Delegate handler) {
      Connect (o, name, false, handler);
    }

    static int g_closure_sizeof = gstsharp_g_closure_sizeof ();

    public static void Connect (GLib.Object o, string name,
                                bool after, Delegate handler) {
      Delegate newHandler;

      ObjectSignalKey k = new ObjectSignalKey (o, name);

      if (!SignalInfo.IsValidDelegate (handler))
        throw new Exception ("Invalid delegate");

      if (SignalHandlers[k] != null) {
        SignalInfo si = (SignalInfo) SignalHandlers[k];
        if (!si.IsCompatibleDelegate (handler))
          throw new Exception ("Incompatible delegate");

        newHandler = Delegate.Combine (si.RegisteredHandler, handler);
        si.UpdateArgsType (handler);
        si.RegisteredHandler = newHandler;
      } else {
        if (!SignalInfo.IsValidDelegate (handler))
          throw new Exception ("Invalid delegate");

        IntPtr closure = g_closure_new_simple (g_closure_sizeof, IntPtr.Zero);
        g_closure_set_meta_marshal (closure, (IntPtr) GCHandle.Alloc (k),  marshalHandler);
        uint signalId = g_signal_connect_closure (o.Handle, name, closure, after);
        SignalHandlers.Add (k, new SignalInfo (signalId, closure, handler));
      }
    }

    [DllImport ("libgstreamersharpglue-1.0.6.so") ]
    static extern int gstsharp_g_closure_sizeof ();

    public static void Disconnect (GLib.Object o, string name, Delegate handler) {
      ObjectSignalKey k = new ObjectSignalKey (o, name);
      if (SignalHandlers[k] != null) {
        SignalInfo si = (SignalInfo) SignalHandlers[k];
        Delegate newHandler = Delegate.Remove (si.RegisteredHandler, handler);
        if (newHandler == null || handler == null) {
          g_signal_handler_disconnect (o.Handle, si.HandlerId);
          SignalHandlers.Remove (k);
        } else {
          si.RegisteredHandler = newHandler;
        }
      }
    }

    static void OnMarshal (IntPtr closure, ref GLib.Value retval, uint argc, IntPtr argsPtr,
                           IntPtr ihint, IntPtr data) {
      object [] args = new object[argc - 1];
      object o = ( (GLib.Value) Marshal.PtrToStructure (argsPtr, typeof (GLib.Value))).Val;

      for (int i = 1; i < argc; i++) {
        IntPtr struct_ptr = (IntPtr) ( (long) argsPtr + (i * gvalue_struct_size));
        GLib.Value argument = (GLib.Value) Marshal.PtrToStructure (struct_ptr, typeof (GLib.Value));
        args[i - 1] = argument.Val;
      }

      if (data == IntPtr.Zero) {
        Console.Error.WriteLine ("No available data");
        return;
      }

      ObjectSignalKey k = (ObjectSignalKey) ( (GCHandle) data).Target;
      if (k != null) {
        SignalInfo si = (SignalInfo) SignalHandlers[k];
        GLib.SignalArgs arg = (GLib.SignalArgs) Activator.CreateInstance (si.ArgsType);
        arg.Args = args;
        si.RegisteredHandler.DynamicInvoke (new object[] {o, arg});
        if (arg.RetVal != null) {
          retval.Val = arg.RetVal;
        }
      }
    }


    [DllImport ("libgobject-2.0-0.dll") ]
    static extern IntPtr g_closure_new_simple (int size, IntPtr data);

    [DllImport ("libgobject-2.0-0.dll") ]
    static extern uint g_signal_connect_closure (IntPtr instance,
        string name, IntPtr closure, bool after);

    [DllImport ("libgobject-2.0-0.dll") ]
    static extern void g_closure_set_meta_marshal (IntPtr closure, IntPtr data, GClosureMarshal marshal);

    class GTypeSignalKey {
      GType type;
      string signal_name;

      public GTypeSignalKey (GType type, string name) {
        this.type = type;
        signal_name = name;
      }

      public override bool Equals (object o) {
        if (o is GTypeSignalKey) {
          GTypeSignalKey k = (GTypeSignalKey) o;
          return k.type.Equals (this.type) && signal_name.Equals (k.signal_name);
        }
        return base.Equals (o);
      }

      public override int GetHashCode() {
        return type.GetHashCode() ^ signal_name.GetHashCode();
      }
    }

    struct SignalQuery {
      public uint signal_id;
      public string signal_name;
      public GType itype;
      public uint signal_flags;
      public GType return_type;
      public uint n_params;
      public Type[] param_types;
    }

    static Hashtable SignalEmitInfo = new Hashtable ();

    public static object Emit (GLib.Object o, string name, params object[] parameters) {
      SignalQuery query;
      IntPtr type = gstsharp_g_type_from_instance (o.Handle);
      GType gtype = new GType (type);
      string signal_name, signal_detail;
      uint signal_detail_quark = 0;
      int colon;

      colon = name.LastIndexOf ("::");

      if (colon == -1) {
        signal_name = name;
        signal_detail = String.Empty;
      } else {
        signal_name = name.Substring (0, colon);
        signal_detail = name.Substring (colon + 2);
      }

      GTypeSignalKey key = new GTypeSignalKey (gtype, signal_name);

      if (SignalEmitInfo[key] == null) {
        IntPtr native_string = GLib.Marshaller.StringToPtrGStrdup (signal_name);
        uint signal_id = g_signal_lookup (native_string, type);
        GLib.Marshaller.Free (native_string);

        if (signal_id == 0)
          throw new NotSupportedException (String.Format ("{0} has no signal of name {1}", o, name));
        GSignalQuery q = new GSignalQuery ();
        g_signal_query (signal_id, ref q);

        if (q.signal_id == 0)
          throw new NotSupportedException (String.Format ("{0} couldn't be queried for signal with name {1}", o, name));

        query = new SignalQuery ();

        query.signal_id = signal_id;
        query.signal_name = GLib.Marshaller.Utf8PtrToString (q.signal_name);
        query.itype = new GType (q.itype);
        query.signal_flags = q.signal_flags;
        query.return_type = new GType (q.return_type);
        query.n_params = q.n_params;
        query.param_types = new Type[q.n_params];

        for (int i = 0; i < query.n_params; i++) {
          IntPtr t = Marshal.ReadIntPtr (q.param_types, i);
          GType g = new GType (t);

          query.param_types[i] = (Type) g;
        }

        SignalEmitInfo.Add (key, query);
      }

      query = (SignalQuery) SignalEmitInfo[key];
      GLib.Value[] signal_parameters = new GLib.Value[query.n_params + 1];
      signal_parameters[0] = new GLib.Value (o);

      if (parameters.Length != query.n_params)
        throw new ApplicationException (String.Format ("Invalid number of parameters: expected {0}, got {1}", query.n_params, parameters.Length));

      for (int i = 0; i < query.n_params; i++) {
        Type expected_type = (Type) query.param_types[i];
        Type given_type = parameters[i].GetType ();

        if (expected_type != given_type && ! given_type.IsSubclassOf (given_type))
          throw new ApplicationException (String.Format ("Invalid parameter type: expected {0}, got {1}", expected_type, given_type));

        signal_parameters[i + 1] = new GLib.Value (parameters[i]);
      }

      GLib.Value return_value = new GLib.Value ();
      if (query.return_type != GType.Invalid && query.return_type != GType.None)
        return_value.Init (query.return_type);

      if (signal_detail != String.Empty) {
        IntPtr native_string = GLib.Marshaller.StringToPtrGStrdup (signal_detail);
        signal_detail_quark = g_quark_from_string (native_string);
        GLib.Marshaller.Free (native_string);
      }

      g_signal_emitv (signal_parameters, query.signal_id, signal_detail_quark, ref return_value);

      foreach (GLib.Value v in signal_parameters)
        v.Dispose ();

      object ret = (query.return_type != GType.Invalid && query.return_type != GType.None) ? return_value.Val : null;

      if (ret != null)
        return_value.Dispose ();

      return ret;
    }

    [DllImport ("libgstreamersharpglue-1.0.6.so") ]
    static extern IntPtr gstsharp_g_type_from_instance (IntPtr o);

    [DllImport ("libgobject-2.0-0.dll") ]
    static extern int g_signal_handler_disconnect (IntPtr o, uint handler_id);

    [DllImport ("libgobject-2.0-0.dll") ]
    static extern uint g_signal_lookup (IntPtr name, IntPtr itype);

    [DllImport ("libglib-2.0-0.dll") ]
    static extern uint g_quark_from_string (IntPtr str);

    [DllImport ("libgobject-2.0-0.dll") ]
    static extern void g_signal_emitv (GLib.Value[] parameters, uint signal_id, uint detail, ref GLib.Value return_value);

    [StructLayout (LayoutKind.Sequential) ]
    struct GSignalQuery {
      public uint signal_id;
      public IntPtr signal_name;
      public IntPtr itype;
      public uint signal_flags;
      public IntPtr return_type;
      public uint n_params;
      public IntPtr param_types;
    }

    [DllImport ("libgobject-2.0-0.dll") ]
    static extern void g_signal_query (uint signal_id, ref GSignalQuery query);
  }
}