﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Framework
{
    /// <summary>
    /// Flexible binder that can handle automatic mappings from <see cref="Stream"/> to various other types.
    /// </summary>
    public abstract class StreamValueBinder : ValueBinder
    {
        private readonly ParameterInfo _parameter;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="parameter">The parameter being bound to.</param>
        /// <param name="bindStepOrder"></param>
        protected StreamValueBinder(ParameterInfo parameter, BindStepOrder bindStepOrder = BindStepOrder.Default)
            : base(parameter.ParameterType, bindStepOrder)
        {
            _parameter = parameter;
        }

        /// <summary>
        /// Gets the set of Types this binder supports. I.e., from the base stream, this binder
        /// will handle conversions to the other types.
        /// </summary>
        public static IEnumerable<Type> SupportedTypes
        {
            get
            {
                return new Type[] 
                { 
                    typeof(Stream), 
                    typeof(TextWriter), 
                    typeof(StreamWriter), 
                    typeof(TextReader), 
                    typeof(StreamReader), 
                    typeof(string), 
                    typeof(byte[]) 
                };
            }
        }

        /// <summary>
        /// Create the stream for this binding.
        /// </summary>
        /// <returns></returns>
        protected abstract Stream GetStream();

        /// <inheritdoc/>
        public override object GetValue()
        {
            if (_parameter.IsOut)
            {
                return null;
            }

            Stream stream = GetStream();

            if (_parameter.ParameterType == typeof(TextWriter) ||
                _parameter.ParameterType == typeof(StreamWriter))
            {
                return new StreamWriter(stream);
            }
            else if (_parameter.ParameterType == typeof(TextReader) ||
                    _parameter.ParameterType == typeof(StreamReader))
            {
                return new StreamReader(stream);
            }
            else if (_parameter.ParameterType == typeof(string))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            else if (_parameter.ParameterType == typeof(byte[]))
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }

            return stream;
        }

        /// <inheritdoc/>
        public override Task SetValueAsync(object value, CancellationToken cancellationToken)
        {
            if (value == null)
            {
                return Task.FromResult(0);
            }

            if (typeof(Stream).IsAssignableFrom(value.GetType()))
            {
                Stream stream = (Stream)value;
                stream.Close();
            }
            else if (typeof(TextWriter).IsAssignableFrom(value.GetType()))
            {
                TextWriter writer = (TextWriter)value;
                writer.Close();
            }
            else if (typeof(TextReader).IsAssignableFrom(value.GetType()))
            {
                TextReader reader = (TextReader)value;
                reader.Close();
            }
            else
            {
                if (_parameter.IsOut)
                {
                    // convert the value as needed into a byte[]
                    byte[] bytes = null;
                    if (value.GetType() == typeof(string))
                    {
                        bytes = Encoding.UTF8.GetBytes((string)value);
                    }
                    else if (value.GetType() == typeof(byte[]))
                    {
                        bytes = (byte[])value;
                    }

                    // open the file using the declared file options, and write the bytes
                    using (Stream stream = GetStream())
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }  
            }

            return Task.FromResult(true);
        }
    }
}
