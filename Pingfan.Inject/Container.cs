using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Pingfan.Inject
{
    /// <summary>
    /// 依赖注入容器
    /// </summary>
    public class Container : IContainer
    {
        private readonly int _currentDeep; // 当前深度, 用于判断递归深度
        private readonly List<InjectPush> _objectItems = new List<InjectPush>();
        internal readonly object Lock = new object();

        private Func<InjectPop, object> _onNotFound = pop =>
            throw new InjectNotRegisteredException($"{pop.Name}未被注册, 类型:{pop.Type}", pop);

        /// <inheritdoc />
        public int MaxDeep { get; set; } = 20;

        /// <inheritdoc />
        public bool IsRoot => Parent == null;

        /// <inheritdoc />
        public IContainer Root => Parent?.Root ?? this;

        /// <inheritdoc />
        public IContainer? Parent { get; }

        /// <inheritdoc />
        public List<IContainer> Children { get; }

        /// <inheritdoc />
        public Func<InjectPop, object> OnNotFound
        {
            get => _onNotFound;
            set
            {
                if (IsRoot == false)
                {
                    throw new Exception("因为找不到会向上搜索, 所有只有根容器才能设置OnNotFound");
                }

                _onNotFound = value;
            }
        }


        /// <summary>
        /// 创建一个容器
        /// </summary>
        /// <param name="parent">默认为根容器</param>
        public Container(Container? parent = null)
        {
            Parent = parent;
            Children = new List<IContainer>();
            this._currentDeep = parent?._currentDeep + 1 ?? 0;
            if (this._currentDeep > this.MaxDeep)
                throw new Exception($"递归深度超过{MaxDeep}层, 可能存在循环依赖");

            if (parent != null)
            {
                this.MaxDeep = parent.MaxDeep;
            }
        }

        /// <inheritdoc />
        public void Push(object instance, string? name = null)
        {
            var type = instance.GetType();
            // lock (((Container)Root).Lock)
            {
                var item = new InjectPush(null, type, name, instance);
                _objectItems.Add(item);
            }
        }


        /// <inheritdoc />
        public void Push<T>(string? name = null)
        {
            var type = typeof(T);
            if (type.IsInterface)
                throw new Exception("无法注入接口");

            // lock (((Container)Root).Lock)
            {
                var item = new InjectPush(null, type, name, null);
                _objectItems.Add(item);
            }
        }

        /// <inheritdoc />
        public void Push<T>(T instance, string? name = null)
        {
            var type = typeof(T);
            if (type.IsInterface)
                throw new Exception("无法注入接口");

            // lock (((Container)Root).Lock)
            {
                var item = new InjectPush(null, type, name, instance);
                _objectItems.Add(item);
            }
        }


        /// <inheritdoc />
        public void Push<TI, T>(T instance, string? name = null) where T : TI
        {
            var interfaceType = typeof(TI);
            var instanceType = typeof(T);
            // lock (((Container)Root).Lock)
            {
                var item = new InjectPush(interfaceType, instanceType, name, instance);
                _objectItems.Add(item);
            }
        }


        /// <inheritdoc />
        public void Push<TI, T>(string? name = null) where T : TI
        {
            var interfaceType = typeof(TI);
            var instanceType = typeof(T);
            // lock (((Container)Root).Lock)
            {
                var item = new InjectPush(interfaceType, instanceType, name, null);
                _objectItems.Add(item);
            }
        }

        /// <inheritdoc />
        public void Push(Type instanceType, string? name = null)
        {
            // 判断instanceType是否是接口
            if (instanceType.IsInterface)
                throw new Exception("无法注入接口");

            // lock (((Container)Root).Lock)
            {
                var item = new InjectPush(null, instanceType, name, null);
                _objectItems.Add(item);
            }
        }

        /// <inheritdoc />
        public void Push(Type interfaceType, object instance, string? name = null)
        {
            Type? instanceType = instance.GetType();

            // 判断instanceType是否是interfaceType的子类
            if (interfaceType.IsAssignableFrom(instanceType))
                throw new Exception($"无法注入 {instanceType} 到 {interfaceType}");

            // lock (((Container)Root).Lock)
            {
                var item = new InjectPush(interfaceType, instanceType, name, instance);
                _objectItems.Add(item);
            }
        }

        /// <inheritdoc />
        public void Push(Type interfaceType, Type instanceType, string? name = null)
        {
            // 判断instanceType的类型是否是interfaceType的子类
            if (interfaceType.IsAssignableFrom(instanceType))
                throw new Exception($"无法注入 {instanceType} 到 {interfaceType}");

            // lock (((Container)Root).Lock)
            {
                var item = new InjectPush(interfaceType, instanceType, name, null);
                _objectItems.Add(item);
            }
        }

        /// <inheritdoc />
        public void Push<T>(Type instanceType, string? name = null)
        {
            var interfaceType = typeof(T);
            if (interfaceType.IsInterface == false)
                throw new Exception("T必须是接口");

            if (interfaceType.IsAssignableFrom(instanceType) == false)
                throw new Exception($"无法注入 {instanceType} 到 {interfaceType}");
            
            // lock (((Container)Root).Lock)
            {
                var item = new InjectPush(interfaceType, instanceType, name, null);
                _objectItems.Add(item);
            }
        }


        /// <inheritdoc />
        public T Get<T>(string? name = null, object? defaultValue = null)
        {
            // lock (Lock)
            {
                return (T)Get(new InjectPop(typeof(T), name, 0, defaultValue));
            }
        }

        /// <inheritdoc />
        public object Get(Type instanceType, string? name = null, object? defaultValue = null)
        {
            // lock (Lock)
            {
                return Get(new InjectPop(instanceType, name, 0, defaultValue));
            }
        }

        /// <inheritdoc />
        public bool Has<T>(string? name = null)
        {
            var type = typeof(T);
            if (type.IsInterface)
            {
                var objectItems = _objectItems.Where(x => x.InterfaceType == type).ToList();
                if (objectItems.Count >= 1) // 找到多个, 用name再匹配一次
                {
                    InjectPush injectPush;
                    if (objectItems.Count > 1)
                        injectPush = objectItems.FirstOrDefault(x =>
                            string.Equals(x.InstanceName, name, StringComparison.OrdinalIgnoreCase)) ?? objectItems[0];
                    else
                        injectPush = objectItems.Last();

                    return injectPush.Instance != null;
                }

                if (Parent != null)
                {
                    // 如果没有找到, 则从父容器中寻找
                    return ((Container)Parent).Has<T>(name);
                }
            }
            else if (type.IsClass)
            {
                var objectItems = _objectItems.Where(x => x.InstanceType == type).ToList();
                if (objectItems.Count >= 1) // 找到多个, 用name再匹配一次
                {
                    InjectPush injectPush;
                    if (objectItems.Count > 1)
                    {
                        injectPush = objectItems.FirstOrDefault(x =>
                            string.Equals(x.InstanceName, name, StringComparison.OrdinalIgnoreCase)) ?? objectItems[0];
                    }
                    else
                    {
                        injectPush = objectItems.Last();
                    }

                    return injectPush != null;
                }

                if (Parent != null)
                {
                    // 如果没有找到, 则从父容器中寻找
                    return ((Container)Parent).Has<T>(name);
                }
            }

            return false;
        }

        /// <inheritdoc />
        public T New<T>() where T : class
        {
            Push<T>();
            return Get<T>();
        }

        /// <inheritdoc />
        public object New(Type type)
        {
            Push(type);
            return Get(type);
        }

        /// <inheritdoc />
        public object Invoke(object instance, MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParametersByCache();
            var parameterValues = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterInfo = parameters[i];
                var attr = parameterInfo.GetCustomAttribute<InjectAttribute>();


                var name = attr?.Name;
                if (string.IsNullOrEmpty(name))
                    name = parameterInfo.Name;


                var defaultValue = attr?.DefaultValue;
                if (defaultValue == null && parameterInfo.HasDefaultValue)
                    defaultValue = parameterInfo.DefaultValue;


                parameterValues[i] = Get(new InjectPop(parameterInfo.ParameterType, name, 0, defaultValue));
            }

            // return method.DynamicInvoke(parameterValues);
            return methodInfo.Invoke(instance, parameterValues);
        }


        private object Get(InjectPop injectPop)
        {
            if (injectPop.Deep > MaxDeep)
                throw new Exception($"递归深度超过{MaxDeep}层, 可能存在循环依赖");

            if (injectPop.Type.IsInterface)
            {
                var objectItems = _objectItems.Where(x => x.InterfaceType == injectPop.Type).ToList();
                if (objectItems.Count >= 1) // 找到多个, 用name再匹配一次
                {
                    InjectPush injectPush;
                    if (objectItems.Count > 1)
                        injectPush = objectItems.FirstOrDefault(x =>
                                         string.Equals(x.InstanceName, injectPop.Name,
                                             StringComparison.OrdinalIgnoreCase)) ??
                                     objectItems[0];
                    else
                        injectPush = objectItems.Last();
                    return Get(new InjectPop(injectPush.InstanceType!, injectPop.Name, ++injectPop.Deep,
                        injectPop.DefaultValue));
                }

                if (Parent != null)
                {
                    // 如果没有找到, 则从父容器中寻找
                    injectPop.Deep++;
                    return ((Container)Parent).Get(injectPop);
                }
            }
            else if (injectPop.Type.IsClass || injectPop.Type.IsValueType)
            {
                var objectItems = _objectItems.Where(x => x.InstanceType == injectPop.Type).ToList();
                if (objectItems.Count >= 1) // 找到多个, 用name再匹配一次
                {
                    InjectPush injectPush;
                    if (objectItems.Count > 1)
                    {
                        injectPush = objectItems.FirstOrDefault(x =>
                                         string.Equals(x.InstanceName, injectPop.Name,
                                             StringComparison.OrdinalIgnoreCase)) ??
                                     objectItems[0];
                    }
                    else // 最后一个
                        injectPush = objectItems.Last();

                    if (injectPush.Instance == null)
                    {
                        // 获取所有的构造函数
                        // var constructors = injectPop.Type.GetConstructors();
                        var constructors = injectPop.Type.GetConstructorsByCache();


                        // 先判断是否有Inject特性
                        var constructorInfo = constructors.FirstOrDefault(p => p.IsDefined(typeof(InjectAttribute)));
                        if (constructorInfo == null)
                            // 获取参数最多的构造函数
                            constructorInfo = constructors.OrderByDescending(p => p.GetParametersByCache().Length)
                                .First();

                        var parameterInfos = constructorInfo.GetParametersByCache();
                        var parameters = new object[parameterInfos.Length];
                        for (var i = 0; i < parameterInfos.Length; i++)
                        {
                            var parameterInfo = parameterInfos[i];
                            {
                                if (parameterInfo.ParameterType == this.GetType())
                                    throw new Exception("无法在构造参数中注入容器, 请使用属性注入");
                            }
                            {
                                // 判断parameterInfo.ParameterType 是否是this的子类
                                // if (parameterInfo.ParameterType.IsAssignableFrom(this.GetType()))
                                // if (parameterInfo.ParameterType.Name == typeof(IContainer).Name)
                                // {
                                //     parameters[i] = this;
                                //     continue;
                                // }
                            }

                            var attr = parameterInfo.GetCustomAttribute<InjectAttribute>();
                            // 获取特性上的名字
                            var name = injectPop.Name;
                            if (string.IsNullOrEmpty(name))
                                name = attr?.Name;
                            else if (string.IsNullOrEmpty(name))
                                name = parameterInfo.Name;

                            var defaultValue = injectPop.DefaultValue;
                            if (defaultValue == null)
                                defaultValue = attr?.DefaultValue;
                            if (defaultValue == null && parameterInfo.HasDefaultValue)
                                defaultValue = parameterInfo.DefaultValue;


                            parameters[i] = Get(new InjectPop(parameterInfo.ParameterType, name, ++injectPop.Deep,
                                defaultValue));
                        }

                        injectPush.Instance = constructorInfo.Invoke(parameters);

                        // 注入属性
                        var properties = injectPop.Type.GetPropertiesByCache()
                            .Where(p => p.IsDefined(typeof(InjectAttribute)));
                        foreach (var property in properties)
                        {
                            // popItem.Deep++;
                            var propertyType = property.PropertyType;
                            // 判断propertyType是否是IContainer的实现类或者子类
                            if (typeof(IContainer).IsAssignableFrom(propertyType))
                            {
                                property.SetValue(injectPush.Instance, this);
                            }
                            else
                            {
                                var attr = property.GetCustomAttribute<InjectAttribute>();

                                // 获取特性上的名字
                                var name = injectPop.Name;
                                if (string.IsNullOrEmpty(name))
                                    name = attr?.Name;
                                else if (string.IsNullOrEmpty(name))
                                    name = property.Name;

                                var defaultValue = injectPop.DefaultValue;
                                if (defaultValue == null)
                                    defaultValue = attr?.DefaultValue;
                         
                                var propertyValue =
                                    Get(new InjectPop(propertyType, name, ++injectPop.Deep, defaultValue));
                                property.SetValue(injectPush.Instance, propertyValue);
                            }
                        }


                        if (injectPush.Instance is IContainerReady containerReady)
                        {
                            containerReady.OnContainerReady();
                        }
                    }


                    return injectPush.Instance!;
                }


                if (Parent != null)
                {
                    // 如果没有找到, 则从父容器中寻找
                    injectPop.Deep++;
                    return ((Container)Parent).Get(injectPop);
                }
            }


            if (injectPop.DefaultValue != null)
                return injectPop.DefaultValue;

            return this.OnNotFound(injectPop);
        }


        /// <inheritdoc />
        public IContainer CreateContainer()
        {
            var child = new Container(this);
            this.Children.Add(child);
            return child;
        }


        /// <inheritdoc />
        public void Dispose()
        {
            lock (((Container)Root).Lock)
            {
                foreach (var child in Children)
                {
                    child.Dispose();
                }

                Children.Clear();


                // 从父类中移除自己
                if (Parent != null)
                    Parent.Children.Remove(this);


                // 释放所有的实例
                foreach (var objectItem in _objectItems)
                {
                    if (objectItem.Instance is IDisposable disposable)
                        disposable.Dispose();
                }

                _objectItems.Clear();
            }
        }
    }
}