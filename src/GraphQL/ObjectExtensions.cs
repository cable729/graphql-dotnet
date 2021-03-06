using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace GraphQL
{
    public static class ObjectExtensions
    {
        public static T ToObject<T>(this IDictionary<string, object> source)
            where T : class, new()
        {
            return (T) ToObject(source, typeof(T));
        }

        public static object ToObject(this IDictionary<string, object> source, Type type)
        {
            var obj = Activator.CreateInstance(type);

            foreach (var item in source)
            {
                var propertyType = type.GetProperty(item.Key,
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (propertyType != null)
                {
                    var value = GetPropertyValue(item.Value, propertyType.PropertyType);
                    propertyType.SetValue(obj, value, null);
                }
            }

            return obj;
        }

        public static object GetPropertyValue(object propertyValue, Type fieldType)
        {
            if (fieldType.Name != "String"
                && fieldType.GetInterface("IEnumerable`1") != null)
            {
                var elementType = fieldType.GetGenericArguments()[0];
                var underlyingType = Nullable.GetUnderlyingType(elementType) ?? elementType;
                var genericListType = typeof(List<>).MakeGenericType(elementType);
                var newArray = (IList) Activator.CreateInstance(genericListType);

                var valueList = propertyValue as IEnumerable;
                if (valueList == null) return newArray;

                foreach (var listItem in valueList)
                {
                    newArray.Add(listItem == null ? null : GetPropertyValue(listItem, underlyingType));
                }

                return newArray;
            }

            var value = propertyValue;

            fieldType = Nullable.GetUnderlyingType(fieldType) ?? fieldType;

            if (fieldType.IsEnum)
            {
                if (value == null)
                {
                    var enumNames = Enum.GetNames(fieldType);
                    value = enumNames[0];
                }

                if (!IsDefinedEnumValue(fieldType, value))
                {
                    throw new ExecutionError($"Unknown value '{value}' for enum '{fieldType.Name}'.");
                }

                var str = value.ToString();
                value = Enum.Parse(fieldType, str, true);
            }

            return GetValue(value, fieldType);
        }

        public static object GetValue(object value, Type fieldType)
        {
            if (value == null) return null;

            var text = value as string;
            return text != null
              ? TypeDescriptor.GetConverter(fieldType).ConvertFromInvariantString(text)
              : Convert.ChangeType(value, fieldType);
        }

        public static bool IsDefinedEnumValue(Type type, object value)
        {
            var names = Enum.GetNames(type);
            if (names.Contains(value?.ToString() ?? "", StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            var underlyingType = Enum.GetUnderlyingType(type);
            var converted = Convert.ChangeType(value, underlyingType);

            var values = Enum.GetValues(type);

            foreach (var val in values)
            {
                var convertedVal = Convert.ChangeType(val, underlyingType);
                if (convertedVal.Equals(converted))
                {
                    return true;
                }
            }

            return false;
        }

        public static IDictionary<string, object> AsDictionary(
            this object source,
            BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
        {
            return source
                .GetType()
                .GetProperties(flags)
                .ToDictionary
                (
                    propInfo => propInfo.Name,
                    propInfo => propInfo.GetValue(source, null)
                );
        }
    }
}
