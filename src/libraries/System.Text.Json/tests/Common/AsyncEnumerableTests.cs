﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public abstract partial class AsyncEnumerableTests : SerializerTests
    {
        public AsyncEnumerableTests(JsonSerializerWrapper serializerWrapper) : base(serializerWrapper)
        {
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(10, 1)]
        [InlineData(100, 1)]
        [InlineData(1000, 1)]
        [InlineData(1000, 1000)]
        [InlineData(1000, 32000)]
        public async Task DeserializeAsyncEnumerable_ReadSimpleObjectAsync(int count, int bufferSize)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultBufferSize = bufferSize
            };

            using var stream = new MemoryStream(GenerateJsonArray(count));

            int callbackCount = 0;
            await foreach (SimpleTestClass item in Serializer.DeserializeAsyncEnumerable<SimpleTestClass>(stream, options))
            {
                Assert.Equal(callbackCount, item.MyInt32);

                item.MyInt32 = 2; // Put correct value back for Verify()
                item.Verify();

                callbackCount++;
            }

            Assert.Equal(count, callbackCount);

            static byte[] GenerateJsonArray(int count)
            {
                SimpleTestClass[] collection = new SimpleTestClass[count];
                for (int i = 0; i < collection.Length; i++)
                {
                    var obj = new SimpleTestClass();
                    obj.Initialize();
                    obj.MyInt32 = i; // verify order correctness
                    collection[i] = obj;
                }

                return JsonSerializer.SerializeToUtf8Bytes(collection);
            }
        }

        [Theory]
        [MemberData(nameof(GetAsyncEnumerableSources))]
        public async Task DeserializeAsyncEnumerable_ReadSourceAsync<TElement>(IEnumerable<TElement> source, int bufferSize, DeserializeAsyncEnumerableOverload overload)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultBufferSize = bufferSize,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            };

            byte[] data = JsonSerializer.SerializeToUtf8Bytes(source);

            using var stream = new MemoryStream(data);
            List<TElement> results = await DeserializeAsyncEnumerableWrapper<TElement>(stream, options, overload: overload).ToListAsync();
            Assert.Equal(source, results);
        }

        [Theory]
        [InlineData(DeserializeAsyncEnumerableOverload.JsonSerializerOptions)]
        [InlineData(DeserializeAsyncEnumerableOverload.JsonTypeInfo)]
        public async Task DeserializeAsyncEnumerable_ShouldStreamPartialData(DeserializeAsyncEnumerableOverload overload)
        {
            string json = JsonSerializer.Serialize(Enumerable.Range(0, 100));

            using var stream = new Utf8MemoryStream(json);
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultBufferSize = 1
            };

            IAsyncEnumerable<int> asyncEnumerable = DeserializeAsyncEnumerableWrapper<int>(stream, options, overload: overload);
            await using IAsyncEnumerator<int> asyncEnumerator = asyncEnumerable.GetAsyncEnumerator();

            for (int i = 0; i < 20; i++)
            {
                bool success = await asyncEnumerator.MoveNextAsync();
                Assert.True(success, "AsyncEnumerator.MoveNextAsync() should return true.");
                Assert.True(stream.Position < stream.Capacity / 2, "should have consumed less than half of the stream contents.");
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task DeserializeAsyncEnumerable_Object_TopLevelValues(int count)
        {
            JsonSerializerOptions options = new() { DefaultBufferSize = 1 };
            string json = GenerateJsonTopLevelValues(count);
            using var stream = new Utf8MemoryStream(json);

            IAsyncEnumerable<SimpleTestClass> asyncEnumerable = Serializer.DeserializeAsyncEnumerable<SimpleTestClass>(stream, topLevelValues:true, options);

            int i = 0;
            await foreach (SimpleTestClass item in asyncEnumerable)
            {
                Assert.Equal(i++, item.MyInt32);
                item.MyInt32 = 2; // Put correct value back for Verify()
                item.Verify();
            }

            Assert.Equal(count, i);

            static string GenerateJsonTopLevelValues(int count)
            {
                StringBuilder sb = new();
                for (int i = 0; i < count; i++)
                {
                    var obj = new SimpleTestClass();
                    obj.Initialize();
                    obj.MyInt32 = i; // verify order correctness

                    sb.Append(JsonSerializer.Serialize(obj));
                    sb.Append((i % 5) switch { 0 => "", 1 => " ", 2 => "\t", 3 => "\r\n", _ => "   \n\n\n\n\n   " });
                }

                return sb.ToString();
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task DeserializeAsyncEnumerable_Array_TopLevelValues(int count)
        {
            JsonSerializerOptions options = new() { DefaultBufferSize = 1 };
            string json = GenerateJsonTopLevelValues(count);
            using var stream = new Utf8MemoryStream(json);

            IAsyncEnumerable<List<int>?> asyncEnumerable = Serializer.DeserializeAsyncEnumerable<List<int>?>(stream, topLevelValues:true, options);

            int i = 0;
            await foreach (List<int>? item in asyncEnumerable)
            {
                switch (i++ % 4)
                {
                    case 0:
                        Assert.Null(item);
                        break;
                    case 1:
                        Assert.Equal([], item);
                        break;
                    case 2:
                        Assert.Equal([1], item);
                        break;
                    case 3:
                        Assert.Equal([1, 2, 3], item);
                        break;
                }
            }

            Assert.Equal(count, i);

            static string GenerateJsonTopLevelValues(int count)
            {
                StringBuilder sb = new();
                for (int i = 0; i < count; i++)
                {
                    sb.Append((i % 4) switch { 0 => " null", 1 => "[]", 2 => "[1]", _ => "[1,2,3]" });
                    sb.Append((i % 5) switch { 0 => "", 1 => " ", 2 => "\t", 3 => "\r\n", _ => "   \n\n\n\n\n   " });
                }

                return sb.ToString();
            }
        }

        [Fact]
        public async Task DeserializeAsyncEnumerable_TopLevelValues_TrailingData_ThrowsJsonException()
        {
            JsonSerializerOptions options = new() { DefaultBufferSize = 1 };
            using var stream = new Utf8MemoryStream("""[] [1] [1,2,3] <NotJson/>""");

            IAsyncEnumerable<List<int>> asyncEnumerable = Serializer.DeserializeAsyncEnumerable<List<int>>(stream, topLevelValues:true, options);
            await using var asyncEnumerator = asyncEnumerable.GetAsyncEnumerator();

            await Assert.ThrowsAnyAsync<JsonException>(async () =>
            {
                while (await asyncEnumerator.MoveNextAsync());
            });
        }

        [Theory]
        [InlineData(DeserializeAsyncEnumerableOverload.JsonSerializerOptions)]
        [InlineData(DeserializeAsyncEnumerableOverload.JsonTypeInfo)]
        public async Task DeserializeAsyncEnumerable_ShouldTolerateCustomQueueConverters(DeserializeAsyncEnumerableOverload overload)
        {
            const int expectedCount = 20;

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                Converters = { new DegenerateQueueConverterFactory() }
            };

            byte[] data = JsonSerializer.SerializeToUtf8Bytes(Enumerable.Repeat(Enumerable.Repeat(1,3), expectedCount));

            using var stream = new MemoryStream(data);

            int callbackCount = 0;
            await foreach (Queue<int> nestedQueue in DeserializeAsyncEnumerableWrapper<Queue<int>>(stream, options, overload: overload))
            {
                Assert.Equal(1, nestedQueue.Count);
                Assert.Equal(0, nestedQueue.Peek());
                callbackCount++;
            }

            Assert.Equal(expectedCount, callbackCount);
        }

        private class DegenerateQueueConverterFactory : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeof(Queue<>) == typeToConvert.GetGenericTypeDefinition();
            public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            {
                Type queueElement = typeToConvert.GetGenericArguments()[0];
                Type converterType = typeof(DegenerateQueueConverter<>).MakeGenericType(queueElement);
                return (JsonConverter)Activator.CreateInstance(converterType, nonPublic: true);
            }

            private class DegenerateQueueConverter<T> : JsonConverter<Queue<T>>
            {
                public override bool CanConvert(Type typeToConvert) => typeof(Queue<T>).IsAssignableFrom(typeToConvert);
                public override Queue<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray);
                    var queue = new Queue<T>();
                    queue.Enqueue(default);
                    return queue;
                }

                public override void Write(Utf8JsonWriter writer, Queue<T> value, JsonSerializerOptions options) => throw new NotImplementedException();
            }
        }

        [Theory]
        [InlineData("42")]
        [InlineData("\"\"")]
        [InlineData("{}")]
        public async Task DeserializeAsyncEnumerable_NotARootLevelJsonArray_ThrowsJsonException(string json)
        {
            using var utf8Json = new Utf8MemoryStream(json);

            {
                IAsyncEnumerable<int> asyncEnumerable = Serializer.DeserializeAsyncEnumerable<int>(utf8Json);
                await using IAsyncEnumerator<int> enumerator = asyncEnumerable.GetAsyncEnumerator();
                await Assert.ThrowsAsync<JsonException>(async () => await enumerator.MoveNextAsync());
            }

            {
                IAsyncEnumerable<int> asyncEnumerable = Serializer.DeserializeAsyncEnumerable(utf8Json, ResolveJsonTypeInfo<int>());
                await using IAsyncEnumerator<int> enumerator = asyncEnumerable.GetAsyncEnumerator();
                await Assert.ThrowsAsync<JsonException>(async () => await enumerator.MoveNextAsync());
            }
        }

        [Theory]
        [InlineData(DeserializeAsyncEnumerableOverload.JsonSerializerOptions)]
        [InlineData(DeserializeAsyncEnumerableOverload.JsonTypeInfo)]
        public async Task DeserializeAsyncEnumerable_CancellationToken_ThrowsOnCancellation(DeserializeAsyncEnumerableOverload overload)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultBufferSize = 1,
            };

            byte[] data = JsonSerializer.SerializeToUtf8Bytes(Enumerable.Range(1, 100));

            var token = new CancellationToken(canceled: true);
            using var stream = new MemoryStream(data);
            var cancellableAsyncEnumerable = DeserializeAsyncEnumerableWrapper<int>(stream, options, token, overload);

            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await foreach (int element in cancellableAsyncEnumerable)
                {
                }
            });
        }

        [Theory]
        [InlineData(DeserializeAsyncEnumerableOverload.JsonSerializerOptions)]
        [InlineData(DeserializeAsyncEnumerableOverload.JsonTypeInfo)]
        public async Task DeserializeAsyncEnumerable_EnumeratorWithCancellationToken_ThrowsOnCancellation(DeserializeAsyncEnumerableOverload overload)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultBufferSize = 1
            };

            byte[] data = JsonSerializer.SerializeToUtf8Bytes(Enumerable.Range(1, 100));

            var token = new CancellationToken(canceled: true);
            using var stream = new MemoryStream(data);
            var cancellableAsyncEnumerable = DeserializeAsyncEnumerableWrapper<int>(stream, options, overload: overload).WithCancellation(token);

            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await foreach (int element in cancellableAsyncEnumerable)
                {
                }
            });
        }

        [Theory]
        [InlineData(5, 1024)]
        [InlineData(5, 1024 * 1024)]
        public async Task DeserializeAsyncEnumerable_SlowStreamWithLargeStrings(int totalStrings, int stringLength)
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new StringLengthConverter() }
            };

            using var stream = new SlowStream(GenerateJsonCharacters());
            string expectedElement = stringLength.ToString(CultureInfo.InvariantCulture);
            IAsyncEnumerable<string?> asyncEnumerable = Serializer.DeserializeAsyncEnumerable<string>(stream, options);

            await foreach (string? value in asyncEnumerable)
            {
                Assert.Equal(expectedElement, value);
            }

            IEnumerable<byte> GenerateJsonCharacters()
            {
                // ["xxx...x","xxx...x",...,"xxx...x"]
                yield return (byte)'[';
                for (int i = 0; i < totalStrings; i++)
                {
                    yield return (byte)'"';
                    for (int j = 0; j < stringLength; j++)
                    {
                        yield return (byte)'x';
                    }
                    yield return (byte)'"';

                    if (i < totalStrings - 1)
                    {
                        yield return (byte)',';
                    }
                }
                yield return (byte)']';
            }
        }

        public static IEnumerable<object[]> GetAsyncEnumerableSources()
        {
            yield return WrapArgs(Enumerable.Empty<int>(), 1, DeserializeAsyncEnumerableOverload.JsonSerializerOptions);
            yield return WrapArgs(Enumerable.Empty<int>(), 1, DeserializeAsyncEnumerableOverload.JsonTypeInfo);
            yield return WrapArgs(Enumerable.Range(0, 20), 1, DeserializeAsyncEnumerableOverload.JsonSerializerOptions);
            yield return WrapArgs(Enumerable.Range(0, 100), 20, DeserializeAsyncEnumerableOverload.JsonSerializerOptions);
            yield return WrapArgs(Enumerable.Range(0, 100).Select(i => $"lorem ipsum dolor: {i}"), 500, DeserializeAsyncEnumerableOverload.JsonSerializerOptions);
            yield return WrapArgs(Enumerable.Range(0, 100).Select(i => $"lorem ipsum dolor: {i}"), 500, DeserializeAsyncEnumerableOverload.JsonTypeInfo);
            yield return WrapArgs(Enumerable.Range(0, 10).Select(i => new { Field1 = i, Field2 = $"lorem ipsum dolor: {i}", Field3 = i % 2 == 0 }), 100, DeserializeAsyncEnumerableOverload.JsonSerializerOptions);
            yield return WrapArgs(Enumerable.Range(0, 10).Select(i => new { Field1 = i, Field2 = $"lorem ipsum dolor: {i}", Field3 = i % 2 == 0 }), 100, DeserializeAsyncEnumerableOverload.JsonTypeInfo);
            yield return WrapArgs(Enumerable.Range(0, 100).Select(i => new { Field1 = i, Field2 = $"lorem ipsum dolor: {i}", Field3 = i % 2 == 0 }), 500, DeserializeAsyncEnumerableOverload.JsonSerializerOptions);

            static object[] WrapArgs<TSource>(IEnumerable<TSource> source, int bufferSize, DeserializeAsyncEnumerableOverload overload) => new object[] { source, bufferSize, overload };
        }

        public enum DeserializeAsyncEnumerableOverload { JsonSerializerOptions, JsonTypeInfo };

        private IAsyncEnumerable<T> DeserializeAsyncEnumerableWrapper<T>(Stream stream, JsonSerializerOptions options = null, CancellationToken cancellationToken = default, DeserializeAsyncEnumerableOverload overload = DeserializeAsyncEnumerableOverload.JsonSerializerOptions)
        {
            return overload switch
            {
                DeserializeAsyncEnumerableOverload.JsonTypeInfo => Serializer.DeserializeAsyncEnumerable<T>(stream, ResolveJsonTypeInfo<T>(options), cancellationToken),
                DeserializeAsyncEnumerableOverload.JsonSerializerOptions or _ => Serializer.DeserializeAsyncEnumerable<T>(stream, options, cancellationToken),
            };
        }

        internal static JsonTypeInfo<T> ResolveJsonTypeInfo<T>(JsonSerializerOptions? options = null)
        {
            return (JsonTypeInfo<T>)ResolveJsonTypeInfo(typeof(T), options);
        }

        private static JsonTypeInfo ResolveJsonTypeInfo(Type type, JsonSerializerOptions? options = null)
        {
            options ??= JsonSerializerOptions.Default;
            options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
            options.MakeReadOnly(); // Lock the options instance before initializing metadata
            return options.TypeInfoResolver.GetTypeInfo(type, options);
        }

        private sealed class SlowStream(IEnumerable<byte> byteSource) : Stream, IDisposable
        {
            private readonly IEnumerator<byte> _enumerator = byteSource.GetEnumerator();
            private long _position;

            public override bool CanRead => true;
            public override int Read(byte[] buffer, int offset, int count)
            {
                Debug.Assert(buffer != null);
                Debug.Assert(offset >= 0 && count <= buffer.Length - offset);

                if (count == 0 || !_enumerator.MoveNext())
                {
                    return 0;
                }

                _position++;
                buffer[offset] = _enumerator.Current;
                return 1;
            }

            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override long Length => throw new NotSupportedException();
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            void IDisposable.Dispose() => _enumerator.Dispose();
        }

        private sealed class StringLengthConverter : JsonConverter<string>
        {
            public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                Debug.Assert(!reader.ValueIsEscaped);
                if (reader.HasValueSequence)
                {
                    return reader.ValueSequence.Length.ToString(CultureInfo.InvariantCulture);
                }
                return reader.ValueSpan.Length.ToString(CultureInfo.InvariantCulture);
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => throw new NotImplementedException();
        }
    }
}
