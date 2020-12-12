using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BinaryX
{
    // Helper methods
    internal static class HelperUtils
    {
        internal static short SwapS16(short value) => (short)(((value >> 8) & 0xFF) | ((value & 0xFF) << 8));
        internal static ushort SwapU16(ushort value) => (ushort)SwapS16((short)value);

        internal static int SwapS32(int value) => ((value >> 24) & 0xFF) | ((value >> 8) & 0xFF00) | ((value & 0xFF00) << 8) | ((value & 0xFF) << 24);
        internal static uint SwapU32(uint value) => (uint)SwapS32((int)value);

        internal static unsafe float SwapF32(float value)
        {
            int temp = SwapS32(*(int*)&value);
            return *(float*)&temp;
        }

        internal static long SwapS64(long value) => ((value >> 56) & 0xFF) | ((value >> 40) & 0xFF00) | ((value >> 24) & 0xFF0000) | ((value >> 8) & 0xFF000000) |
            ((value & 0xFF000000) << 8) | ((value & 0xFF0000) << 24) | ((value & 0xFF00) << 40) | ((value & 0xFF) << 56);
        internal static ulong SwapU64(ulong value) => (ulong)SwapS64((long)value);

        internal static unsafe double SwapF64(double value)
        {
            long temp = SwapS64(*(long*)&value);
            return *(double*)&temp;
        }

        private static readonly Dictionary<Type, Func<dynamic, dynamic>> _endiannessSwapDict = new Dictionary<Type, Func<dynamic, dynamic>>
        {
            { typeof(bool), (dynamic val) => val },
            { typeof(sbyte), (dynamic val) => val },
            { typeof(byte), (dynamic val) => val },
            { typeof(short), (dynamic val) => SwapS16(val) },
            { typeof(ushort), (dynamic val) => SwapU16(val) },
            { typeof(int), (dynamic val) => SwapS32(val) },
            { typeof(uint), (dynamic val) => SwapU32(val) },
            { typeof(float), (dynamic val) => SwapF32(val) },
            { typeof(long), (dynamic val) => SwapS64(val) },
            { typeof(ulong), (dynamic val) => SwapU64(val) },
            { typeof(double), (dynamic val) => SwapF64(val) },
        };

        internal static unsafe T ToStruct2<T>(this IEnumerable<byte> buffer, ByteOrder defaultEndianness = ByteOrder.Undefined, int offset = 0) where T : struct
        {
            if (buffer == null) throw new ArgumentNullException($"{nameof(buffer)} cannot be null!");
            if (offset < 0) throw new ArgumentOutOfRangeException($"{nameof(offset)} cannot be less than 0!");

            byte[] bufferAsArray;

            if (buffer is byte[] localBuffer)
            {
                bufferAsArray = localBuffer;
            }
            else
            {
                bufferAsArray = buffer.ToArray();
            }

            T structure;
            fixed (byte* bufferPointer = bufferAsArray)
            {
                structure = Marshal.PtrToStructure<T>((IntPtr)(bufferPointer + offset));
            }

            // Sort endianness
            object boxedStruct = structure;
            var structEndian = typeof(T).GetCustomAttribute<Endianness>()?.ByteOrder ?? ByteOrder.Undefined;
            foreach (var field in typeof(T).GetFields())
            {
                var propEndianness = field.GetCustomAttribute<Endianness>(true)?.ByteOrder ?? ByteOrder.Undefined;
                if (field.GetCustomAttribute(typeof(FixedBufferAttribute), false) != null)
                    continue;
                if (propEndianness == ByteOrder.BigEndian ||
                    (propEndianness != ByteOrder.LittleEndian && structEndian == ByteOrder.BigEndian) ||
                    (propEndianness == ByteOrder.Undefined && structEndian == ByteOrder.Undefined && defaultEndianness == ByteOrder.BigEndian))
                {
                    var boxedObject = (dynamic)field.GetValue(boxedStruct);
                    Type t = boxedObject.GetType();
                    if (t.BaseType == typeof(Enum))
                        t = Enum.GetUnderlyingType(t);

                    field.SetValue(boxedStruct, _endiannessSwapDict[t](boxedObject));
                }
            }

            return (T)boxedStruct;
        }

        internal static unsafe T ToStruct<T>(this IEnumerable<byte> buffer, ByteOrder defaultEndianness = ByteOrder.Undefined, int offset = 0) where T : struct
        {
            if (buffer == null) throw new ArgumentNullException($"{nameof(buffer)} cannot be null!");
            if (offset < 0) throw new ArgumentOutOfRangeException($"{nameof(offset)} cannot be less than 0!");

            byte[] bufferAsArray;

            if (buffer is byte[] localBuffer)
            {
                bufferAsArray = localBuffer;
            }
            else
            {
                bufferAsArray = buffer.ToArray();
            }

            T structure;
            fixed (byte* bufferPointer = bufferAsArray)
            {
                structure = Marshal.PtrToStructure<T>((IntPtr)(bufferPointer + offset));
            }

            // Sort endianness
            object boxedStruct = structure;
            var structEndian = typeof(T).GetCustomAttribute<Endianness>()?.ByteOrder ?? ByteOrder.Undefined;
            foreach (var field in typeof(T).GetFields())
            {
                var propEndianness = field.GetCustomAttribute<Endianness>(true)?.ByteOrder ?? ByteOrder.Undefined;
                if (field.GetCustomAttribute(typeof(FixedBufferAttribute), false) != null)
                    continue;
                if (propEndianness == ByteOrder.BigEndian ||
                    (propEndianness != ByteOrder.LittleEndian && structEndian == ByteOrder.BigEndian) ||
                    (propEndianness == ByteOrder.Undefined && structEndian == ByteOrder.Undefined && defaultEndianness == ByteOrder.BigEndian))
                {
                    var boxedObject = (dynamic)field.GetValue(boxedStruct);
                    var type = boxedObject.GetType();
                    if (type.BaseType == typeof(Enum))
                        type = Enum.GetUnderlyingType(type);
                    if (type == typeof(sbyte) || type == typeof(byte) || type == typeof(bool))
                        field.SetValue(boxedStruct, boxedObject);
                    else if (type == typeof(float))
                    {
                        float f = (float)boxedObject;
                        float* val = &f;
                        int temp = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(*(int*)val);
                        var value = *(float*)&temp;
                        field.SetValue(boxedStruct, value);
                    }
                    else
                    {
                        var value = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(boxedObject);
                        field.SetValue(boxedStruct, value);
                    }
                }
            }

            return (T)boxedStruct;
        }

        internal static unsafe IEnumerable<byte> ToBytes<T>(this T structure, ByteOrder defaultEndianness = ByteOrder.Undefined) where T : struct
        {
            // Sort endianness
            object boxedStruct = structure;
            var structEndian = typeof(T).GetCustomAttribute<Endianness>()?.ByteOrder ?? ByteOrder.Undefined;
            foreach (var field in typeof(T).GetFields())
            {
                var propEndianness = field.GetCustomAttribute<Endianness>(true)?.ByteOrder ?? ByteOrder.Undefined;
                if (propEndianness == ByteOrder.BigEndian ||
                    (propEndianness != ByteOrder.LittleEndian && structEndian == ByteOrder.BigEndian) || 
                    (propEndianness == ByteOrder.Undefined && structEndian == ByteOrder.Undefined && defaultEndianness == ByteOrder.BigEndian))
                {
                    var boxedObject = (dynamic)field.GetValue(boxedStruct);
                    var type = boxedObject.GetType();
                    if (type.BaseType == typeof(Enum))
                        type = Enum.GetUnderlyingType(type);
                    if (type == typeof(sbyte) || type == typeof(byte) || type == typeof(bool))
                        field.SetValue(boxedStruct, field.GetValue(boxedStruct));
                    else if (type == typeof(float))
                    {
                        float f = (float)boxedObject;
                        float* val = &f;
                        int temp = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(*(int*)val);
                        var value = *(float*)&temp;
                        field.SetValue(boxedStruct, value);
                    }
                    else
                    {
                        var value = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(
                            (dynamic)field.GetValue(boxedStruct));
                        field.SetValue(boxedStruct, value);
                    }
                }
            }

            // Convert the structure directly to a byte buffer
            T swappedStruct = (T)boxedStruct;
            var buffer = new byte[Marshal.SizeOf<T>()];
            fixed (byte* bufferPointer = buffer)
            {
                Marshal.StructureToPtr(swappedStruct, (IntPtr)bufferPointer, false);
            }

            return buffer;
        }
    }

    /// <summary>
    /// Represents which endian format to read as
    /// </summary>
    public enum ByteOrder
    {
        LittleEndian = 0,
        BigEndian = 1,
        Undefined = 2
    }

    /// <summary>
    /// A struct attribute that assigns endianness to a single field or an entire structure
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct)]
    public sealed class Endianness : Attribute
    {
        public ByteOrder ByteOrder;

        public Endianness(ByteOrder byteOrder)
        {
            ByteOrder = byteOrder;
        }
    }

    /// <summary>
    /// A Binary Reader capable of reading both big and little endian data types.
    /// </summary>
    public sealed class BinaryReaderX : BinaryReader
    {
        public ByteOrder ByteOrder = ByteOrder.LittleEndian;

        public long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public BinaryReaderX(Stream input) : base(input) { }

        public BinaryReaderX(Stream input, ByteOrder byteOrder) : this(input)
        {
            ByteOrder = byteOrder;
        }

        public long Seek(long offset, SeekOrigin origin = SeekOrigin.Begin) => BaseStream.Seek(offset, origin);

        public override short ReadInt16() =>
            ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapS16(base.ReadInt16()) : base.ReadInt16();
        public short ReadInt16(ByteOrder order) =>
            order == ByteOrder.BigEndian ? HelperUtils.SwapS16(base.ReadInt16()) : base.ReadInt16();

        public override ushort ReadUInt16() =>
            ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapU16(base.ReadUInt16()) : base.ReadUInt16();
        public ushort ReadUInt16(ByteOrder order) =>
            order == ByteOrder.BigEndian ? HelperUtils.SwapU16(base.ReadUInt16()) : base.ReadUInt16();

        public override int ReadInt32() =>
            ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapS32(base.ReadInt32()) : base.ReadInt32();
        public int ReadInt32(ByteOrder order) =>
           order == ByteOrder.BigEndian ? HelperUtils.SwapS32(base.ReadInt32()) : base.ReadInt32();

        public override uint ReadUInt32() =>
            ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapU32(base.ReadUInt32()) : base.ReadUInt32();
        public uint ReadUInt32(ByteOrder order) =>
            order == ByteOrder.BigEndian ? HelperUtils.SwapU32(base.ReadUInt32()) : base.ReadUInt32();

        public override float ReadSingle() =>
            ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapF32(base.ReadSingle()) : base.ReadSingle();
        public float ReadSingle(ByteOrder order) =>
            order == ByteOrder.BigEndian ? HelperUtils.SwapF32(base.ReadSingle()) : base.ReadSingle();

        public override long ReadInt64() =>
            ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapS64(base.ReadInt64()) : base.ReadInt64();
        public long ReadInt64(ByteOrder order) =>
            order == ByteOrder.BigEndian ? HelperUtils.SwapS64(base.ReadInt64()) : base.ReadInt64();

        public override ulong ReadUInt64() =>
            ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapU64(base.ReadUInt64()) : base.ReadUInt64();
        public ulong ReadUInt64(ByteOrder order) =>
            order == ByteOrder.BigEndian ? HelperUtils.SwapU64(base.ReadUInt64()) : base.ReadUInt64();

        public override double ReadDouble() =>
            ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapF64(base.ReadDouble()) : base.ReadDouble();
        public double ReadDouble(ByteOrder order) =>
            order == ByteOrder.BigEndian ? HelperUtils.SwapF64(base.ReadDouble()) : base.ReadDouble();

        public T ReadStruct<T>() where T : struct => ReadBytes(Marshal.SizeOf<T>()).ToStruct2<T>(ByteOrder);
        public T ReadStruct<T>(ByteOrder order) where T : struct => ReadBytes(Marshal.SizeOf<T>()).ToStruct2<T>(order);
    }

    public sealed class BinaryWriterX : BinaryWriter
    {
        public ByteOrder ByteOrder = ByteOrder.LittleEndian;

        public long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public BinaryWriterX(Stream input) : base(input) { }

        public BinaryWriterX(Stream input, ByteOrder byteOrder) : this(input)
        {
            ByteOrder = byteOrder;
        }

        public long Seek(long offset, SeekOrigin origin = SeekOrigin.Begin) => BaseStream.Seek(offset, origin);

        public override void Write(short value) => base.Write(ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapS16(value) : value);
        public void Write(short value, ByteOrder order) => base.Write(order == ByteOrder.BigEndian ? HelperUtils.SwapS16(value) : value);

        public override void Write(ushort value) => base.Write(ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapU16(value) : value);
        public void Write(ushort value, ByteOrder order) => base.Write(order == ByteOrder.BigEndian ? HelperUtils.SwapU16(value) : value);

        public override void Write(int value) => base.Write(ByteOrder == ByteOrder.BigEndian? HelperUtils.SwapS32(value) : value);
        public void Write(int value, ByteOrder order) => base.Write(order == ByteOrder.BigEndian ? HelperUtils.SwapS32(value) : value);

        public override void Write(uint value) => base.Write(ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapU32(value) : value);
        public void Write(uint value, ByteOrder order) => base.Write(order == ByteOrder.BigEndian ? HelperUtils.SwapU32(value) : value);

        public override void Write(float value) => base.Write(ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapF32(value) : value);
        public void Write(float value, ByteOrder order) => base.Write(order == ByteOrder.BigEndian ? HelperUtils.SwapF32(value) : value);

        public override void Write(long value) => base.Write(ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapS64(value) : value);
        public void Write(long value, ByteOrder order) => base.Write(order == ByteOrder.BigEndian ? HelperUtils.SwapS64(value) : value);

        public override void Write(ulong value) => base.Write(ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapU64(value) : value);
        public void Write(ulong value, ByteOrder order) => base.Write(order == ByteOrder.BigEndian ? HelperUtils.SwapU64(value) : value);

        public override void Write(double value) => base.Write(ByteOrder == ByteOrder.BigEndian ? HelperUtils.SwapF64(value) : value);
        public void Write(double value, ByteOrder order) => base.Write(order == ByteOrder.BigEndian ? HelperUtils.SwapF64(value) : value);

        public void WriteStruct<T>(T structure) where T : struct => base.Write(structure.ToBytes(ByteOrder) as byte[] ?? Array.Empty<byte>());
        public void WriteStruct<T>(T structure, ByteOrder order) where T : struct => base.Write(structure.ToBytes(order) as byte[] ?? Array.Empty<byte>());
    }

    internal static class StructReader
    {
        internal static T ReadStruct<T>(Stream streamHandle, int readOffset = 0) where T : struct
        {
            var buffer = new byte[Marshal.SizeOf<T>()];
            var structure = default(T);

            try
            {
                if (streamHandle.Length >= readOffset + buffer.Length)
                {
                    streamHandle.Read(buffer, readOffset, buffer.Length);
                    structure = buffer.ToStruct<T>();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

            return structure;
        }
    }

    internal static class StructWriter
    {
        internal static byte[] WriteStruct<T>(T structure, Stream streamHandle = null, int writeOffset = 0) where T : struct
        {
            try
            {
                var buffer = structure.ToBytes() as byte[] ?? new byte[0];
                streamHandle?.Write(buffer, writeOffset, buffer.Length);

                return buffer;
            }
            catch
            {
                return null;
            }
        }
    }
}
