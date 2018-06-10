using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SQLite.Net.Attributes;

namespace Database
{
    public class ObjectHelper
    {
        public static PropertyInfo[] GetReflectionProperties(object obj, bool SkipIgnoreFields = false)
        {
            // All fields
            List<PropertyInfo> all = new List<PropertyInfo>(obj.GetType().GetProperties());
            if (SkipIgnoreFields)
            {
                // Ignore fields
                List<PropertyInfo> ignored = obj.GetType().GetProperties()
                   .Where(property =>
                          property.GetCustomAttributes(false)
                                  .OfType<IgnoreAttribute>()
                                  .Any()
                         ).ToList();
                for (int i = 0; i < ignored.Count; i++)
                {
                    if (all.Contains(ignored[i]))
                    {
                        all.Remove(ignored[i]);
                    }
                }
            }
           
            return all.ToArray();
        }

        public static PropertyInfo GetField(object obj, string fieldName)
        {
            PropertyInfo[] props = ObjectHelper.GetReflectionProperties(obj);

            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].Name == fieldName)
                {
                    return props[i];
                };
            }

            return null;

        }


        public static string ToString(object obj)
        {
            if (obj == null)
            {
                return "";
            }

            PropertyInfo[] propertyInfos = GetReflectionProperties(obj, true);

            var sb = new StringBuilder();

            foreach (var info in propertyInfos)
            {
                var value = info.GetValue(obj, null) ?? "(null)";
                sb.AppendLine(info.Name + ": " + value.ToString());
            }


            return sb.ToString();
        }

        public static string GetType(object obj)
        {
            if (obj == null) return "Object Type Null";
            return obj.GetType().Name;
        }

        public static string GetType<T>()
        {
            return typeof(T).Name;
        }

        public static string GetCaller([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            return memberName;
        }
    }
}
