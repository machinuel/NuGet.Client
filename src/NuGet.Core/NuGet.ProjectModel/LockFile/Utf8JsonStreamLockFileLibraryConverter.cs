// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using System.Text.Json;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// A <see cref="Utf8JsonStreamReaderConverter{T}"/> to allow read JSON into <see cref="LockFileLibrary"/>
    /// </summary>
    internal class Utf8JsonStreamLockFileLibraryConverter : Utf8JsonStreamReaderConverter<LockFileLibrary>
    {
        private static readonly byte[] Utf8Sha512 = Encoding.UTF8.GetBytes("sha512");
        private static readonly byte[] Utf8Type = Encoding.UTF8.GetBytes("type");
        private static readonly byte[] Utf8Path = Encoding.UTF8.GetBytes("path");
        private static readonly byte[] Utf8MsbuildProject = Encoding.UTF8.GetBytes("msbuildProject");
        private static readonly byte[] Utf8Servicable = Encoding.UTF8.GetBytes("servicable");
        private static readonly byte[] Utf8HasTools = Encoding.UTF8.GetBytes("hasTools");
        private static readonly byte[] Utf8Files = Encoding.UTF8.GetBytes("files");
        private static readonly char[] Separators = new[] { '/' };

        public override LockFileLibrary Read(ref Utf8JsonStreamReader reader)
        {

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName, found " + reader.TokenType);
            }

            var lockFileLibrary = new LockFileLibrary();
            //We want to read the property name right away
            var propertyName = reader.GetString();
            var parts = propertyName.Split(Separators, 2);
            lockFileLibrary.Name = parts[0];
            if (parts.Length == 2)
            {
                lockFileLibrary.Version = NuGetVersion.Parse(parts[1]);
            }

            reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject, found " + reader.TokenType);
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals(Utf8Type))
                {
                    lockFileLibrary.Type = reader.ReadNextTokenAsString();
                }
                else if (reader.ValueTextEquals(Utf8Path))
                {
                    lockFileLibrary.Path = reader.ReadNextTokenAsString();
                }
                else if (reader.ValueTextEquals(Utf8MsbuildProject))
                {
                    lockFileLibrary.MSBuildProject = reader.ReadNextTokenAsString();
                }
                else if (reader.ValueTextEquals(Utf8Sha512))
                {
                    lockFileLibrary.Sha512 = reader.ReadNextTokenAsString();
                }
                else if (reader.ValueTextEquals(Utf8Servicable))
                {
                    reader.Read();
                    lockFileLibrary.IsServiceable = reader.GetBoolean();
                }
                else if (reader.ValueTextEquals(Utf8HasTools))
                {
                    reader.Read();
                    lockFileLibrary.HasTools = reader.GetBoolean();
                }
                else if (reader.ValueTextEquals(Utf8Files))
                {
                    reader.Read();
                    reader.ReadStringArrayAsIList(lockFileLibrary.Files);
                }
                else
                {
                    reader.TrySkip();
                }
            }
            return lockFileLibrary;
        }
    }
}