using System;

namespace ComponentManagerAPI.GeneralExtensions
{
    public static class TypeExtensions
    {

        public static bool IsPrimitive(this Type self)
        {

            //is primitive or string or deci or date
            bool isPrimitive = self.IsPrimitive
                    || self.Equals(typeof(string))
                    || self.Equals(typeof(decimal))
                    || self.Equals(typeof(DateTime))
                    || typeof(Enum).IsAssignableFrom(self);

            return isPrimitive;

        }
    }
}
