#include <glib-object.h>

gint
gstsharp_g_closure_sizeof (void)
{
  return sizeof (GClosure);
}

GType
gstsharp_g_type_from_instance (GTypeInstance * instance)
{
  return G_TYPE_FROM_INSTANCE (instance);
}

GObjectClass*
gstsharp_gobjectgetclass (gpointer ptr)
{
   return G_OBJECT_GET_CLASS (G_OBJECT(ptr));
}
