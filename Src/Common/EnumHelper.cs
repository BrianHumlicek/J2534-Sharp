using System;
using System.ComponentModel;
using System.Reflection;

namespace Common
{
    public static class EnumHelper
    {
        public static string GetTypeDescription(Enum enumeration)
        {
            var enumerationType = enumeration.GetType();
            var attribute = enumerationType.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? enumerationType.ToString();
        }

        public static string GetMemberDescription(Enum enumeration)
        {
            var member = enumeration.GetType().GetMember(enumeration.ToString())[0];
            var attribute = member.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? enumeration.ToString();
        }
    }
}
