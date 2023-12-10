using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Pingfan.Inject
{
    class InjectParameterInfoCache
    {
        // 线程安全的集合
        private static ConcurrentDictionary<MethodInfo, ParameterInfo[]> ParameterInfoCache { get; } =
            new ConcurrentDictionary<MethodInfo, ParameterInfo[]>();

        public static ParameterInfo[] GetParameters(MethodInfo methodInfo)
        {
            if (ParameterInfoCache.TryGetValue(methodInfo, out var parameterInfos))
                return parameterInfos;

            parameterInfos = methodInfo.GetParameters();
            ParameterInfoCache.TryAdd(methodInfo, parameterInfos);
            return parameterInfos;
        }
    }
    
    class InjectConstructorInfoCache
    {
        // 线程安全的集合
        private static ConcurrentDictionary<Type, ConstructorInfo[]> ConstructorInfoCache { get; } =
            new ConcurrentDictionary<Type, ConstructorInfo[]>();

        public static ConstructorInfo[] GetConstructors(Type type)
        {
            if (ConstructorInfoCache.TryGetValue(type, out var constructorInfos))
                return constructorInfos;

            constructorInfos = type.GetConstructors();
            ConstructorInfoCache.TryAdd(type, constructorInfos);
            return constructorInfos;
        }
    }
    
    class InjectPropertyInfoCache
    {
        // 线程安全的集合
        private static ConcurrentDictionary<Type, PropertyInfo[]> PropertyInfoCache { get; } =
            new ConcurrentDictionary<Type, PropertyInfo[]>();

        public static PropertyInfo[] GetProperties(Type type)
        {
            if (PropertyInfoCache.TryGetValue(type, out var propertyInfos))
                return propertyInfos;

            propertyInfos = type.GetProperties();
            PropertyInfoCache.TryAdd(type, propertyInfos);
            return propertyInfos;
        }
    }
    
    
}