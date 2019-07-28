﻿using System;

namespace Amg.Build
{
    /// <summary>
    /// Identifies an invocation of a target with a certain input.
    /// </summary>
    public class JobId
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">Name of the target</param>
        /// <param name="input">input value for the target</param>
        public JobId(string name, object input)
        {
            Name = name;
            Input = input;
        }

        /// <summary>
        /// Put a prefix to the name to distinguish sub targets
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public JobId Prefix(object prefix)
        {
            return new JobId(prefix + Name, Input);
        }

        string Name { get; }
        object Input { get; }

        /// <summary />
        public override string ToString()
        {
            return Input is Nothing
                ? Name
                : $"{Name}({Input})";
        }

        /// <summary />
        public override bool Equals(object obj)
        {
            return (obj is JobId r)
                ? Name.Equals(r.Name) && Input.Equals(r.Input)
                : false;
        }

        /// <summary />
        public override int GetHashCode()
        {
            return 23 * Name.GetHashCode() + Input.GetHashCode();
        }
    }
}