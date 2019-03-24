﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiContainer.Core {
    public class DiConfiguration {

        public DiConfiguration()
        {
            Configuration = new List<ContainerEntity>();
        }

        public List<ContainerEntity> Configuration { get; private set; }
        
        public void RegisterTransient<TInterface, TImplementation>() 
            where TInterface : class
            where TImplementation : class
        {
            RegisterCore<TInterface, TImplementation>(ObjLifetime.Transient);
        }

        public void RegisterSingleton<TInterface, TImplementation>()
            where TInterface : class 
            where TImplementation : class
        {
            RegisterCore<TInterface, TImplementation>(ObjLifetime.Singleton);
        }

        public ObjLifetime GetObjectLifeTime(Type implType)
        {
            return (from conf in Configuration from impl in conf.Implementations where impl.ImplType == implType select impl.LifeTime).First();
        }

        private void RegisterCore<TInterface, TImplementation>(ObjLifetime lifetime)
        {
            var interfaceType = typeof(TInterface);

            var implementationType = typeof(TImplementation);

            if (!interfaceType.IsAssignableFrom(implementationType))
                throw new InvalidOperationException($"Type {implementationType.ToString()} is not assignable from {interfaceType.ToString()}");

            if (implementationType.IsAbstract || implementationType.IsInterface)
                throw new InvalidOperationException($"Type {implementationType.ToString()} couldn't be abstract or interface.");

            var entity =
                from x in Configuration
                where x.InterfaceType == interfaceType
                select x;

            if (entity.Count().Equals(0))
            {
                Configuration.Add(new ContainerEntity()
                {
                    Implementations = new List<Implementation>()
                    {
                        new Implementation()
                        {
                            ImplType = implementationType,
                            LifeTime = lifetime
                        }
                    },
                    InterfaceType = interfaceType
                });

                return;
            }

            if (entity.First().Implementations.FirstOrDefault(x => x.ImplType == implementationType) != null)
                return;

            entity.First().Implementations.Add(new Implementation() { ImplType = implementationType, LifeTime = lifetime });
        }
    }
}