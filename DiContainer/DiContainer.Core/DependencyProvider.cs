﻿using DiContainer.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiContainer.Core {
    public class DependencyProvider {
        private DiConfiguration m_Configuration { get; set; }

        private List<CreatedObject> CreatedObjects { get; set; }

        public DependencyProvider(DiConfiguration config)
        {
            m_Configuration = config;
            CreatedObjects = new List<CreatedObject>();
        }

        public TInterface Resolve<TInterface>()
        {
            return (TInterface)ResolveCore(typeof(TInterface));
        }

        public Object ResolveCore(Type interfaceType)
        {
            bool IsEnumerable = false;

            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                interfaceType = interfaceType.GetGenericArguments()[0];
                IsEnumerable = true;
            }

            var entity = 
                m_Configuration
                .Configuration
                .Find(x => x.InterfaceType == interfaceType) 
                ?? //if no type was found, probably we are using open generics. If not, entity was not registered.
                m_Configuration
                .Configuration
                .Find(x => x.InterfaceType == interfaceType.GetGenericTypeDefinition())
                ;


            if (entity == null)
                throw new InvalidOperationException($"Dependency {interfaceType.ToString()} was not registered in container.");

            bool IsOpenGenerics = interfaceType.IsGenericType && !entity.InterfaceType.IsConstructedGenericType;

            var result = new List<Object>();

            var implCount = entity.Implementations?.Count();

            for (int implementation = 0; implCount != null && implementation < implCount; ++implementation)
            {
                result.Add(CreateObjectRecursive(entity.Implementations[implementation].ImplType, interfaceType, IsOpenGenerics));
            }

            if (IsEnumerable)
                return result.ListObjToEnumerableType(interfaceType);
            else
                return result.First();
        }

        private Object CreateObjectRecursive(Type classType, Type interfaceType, bool IsOpenGenerics)
        {
            var constructorDependencies = GetConstructorDependencies(classType, interfaceType, IsOpenGenerics);

            if (constructorDependencies.Count().Equals(0))
              return CreateObjectCore(null, classType, interfaceType, IsOpenGenerics);

            var parametersToPass = new object[constructorDependencies.Count()];

            for (int dependency = 0; dependency < constructorDependencies.Count(); ++dependency)
            {
                parametersToPass[dependency] = ResolveCore(constructorDependencies[dependency]);
            }

            return CreateObjectCore(parametersToPass, classType, interfaceType, IsOpenGenerics);
        }

        private Object CreateObjectCore(object[] constructorParams, Type implementationType, Type interfaceType, bool IsOpenGenerics)
        {
            var lifeTime = m_Configuration.GetObjectLifeTime(implementationType);

            if (lifeTime == ObjLifetime.Transient)
            {
                if (IsOpenGenerics)
                    return CreateGenericObject(constructorParams, implementationType, interfaceType, lifeTime);

                return CreateObjInternal(constructorParams, implementationType, interfaceType, lifeTime);
            }
            else if (lifeTime == ObjLifetime.Singleton)
            {
                if (IsObjectCreated(implementationType))
                    return GetCreatedObject(implementationType);

                if (IsOpenGenerics)
                    return CreateGenericObject(constructorParams, implementationType, interfaceType, lifeTime);

                return CreateObjInternal(constructorParams, implementationType, interfaceType, lifeTime);
            }

            throw new NotImplementedException();
        }

        private Object CreateGenericObject(object[] constructorParams, Type implementationType, Type interfaceType, ObjLifetime lifetime)
        {
            //Set object type arguments to ones mentioned in container.
            return CreateObjInternal(constructorParams, implementationType.MakeGenericType(interfaceType.GenericTypeArguments), interfaceType, lifetime);
        }

        private Object CreateObjInternal(object[] constructorParams, Type implementationType, Type interfaceType, ObjLifetime lifetime)
        {
            object createdObj;

            if (constructorParams == null)
            {
                createdObj = Activator.CreateInstance(implementationType);
            }
            else
            {
                createdObj = GetConstructor(implementationType).Invoke(constructorParams);
            }

            CreatedObjects.Add(new CreatedObject()
            {
                ObjType = implementationType,
                Interface = interfaceType,
                SingletonInstance = (lifetime == ObjLifetime.Singleton) ? createdObj : null
            });

            return createdObj;
        }

        private bool IsObjectCreated(Type t)
        {
            return CreatedObjects.Find(x => x.ObjType == t) != null;
        }

        private Object GetCreatedObject(Type t)
        {
            return CreatedObjects.Find(x => x.ObjType == t);
        }

        private List<Type> GetConstructorDependencies(Type classType, Type interfaceType, bool IsOpenGenerics)
        {
            var constructor = GetConstructor(classType);

            if (constructor == null)
                return null;

            var dependencies = constructor
            .GetParameters()
            .Select(x => x.ParameterType)
            .ToList();

            if (!IsOpenGenerics)
                return dependencies;

            var result = new List<Type>();

            foreach(var dependency in dependencies)
            {
                if (!dependency.IsGenericParameter)
                {
                    result.Add(dependency);
                    continue;
                }

                var resolvedArgs = interfaceType.GetGenericArguments();
                var genericArgs = interfaceType.GetGenericTypeDefinition().GetGenericArguments();
                int index = 0;

                if ((index = Array.FindIndex(genericArgs, x => x.Name == dependency.Name)) == -1)
                    throw new ArgumentException("Dependency in constructor was not present in the interface.");

                //if there's more than 1 constraint, obviously, it won't work

                var constraints = genericArgs[index].GetGenericParameterConstraints();

                result.Add(constraints[0]);
            }

            return result;
        }

        private ConstructorInfo GetConstructor(Type classType)
        {
            var constructors = classType.GetConstructors();
            return constructors.Length == 0 ? null : constructors[0];
        }
    }
}
