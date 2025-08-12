using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DataTables
{
    /// <summary>
    /// 类型和名称的组合值。
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct TypeNamePair : IEquatable<TypeNamePair>
    {
        /// <summary>
        /// 初始化类型和名称的组合值的新实例。
        /// </summary>
        /// <param name="type">类型。</param>
        public TypeNamePair(Type type) : this(type, string.Empty)
        {
        }

        /// <summary>
        /// 初始化类型和名称的组合值的新实例。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <param name="name">名称。</param>
        public TypeNamePair(Type type, string name)
        {
            Type = type ?? throw new Exception("Type is invalid.");
            Name = name;
        }

        /// <summary>
        /// 获取类型。
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// 获取名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 获取类型和名称的组合值字符串。
        /// </summary>
        /// <returns>类型和名称的组合值字符串。</returns>
        public override string ToString()
        {
            if (Type == null)
            {
                throw new Exception("Type is invalid.");
            }

            string typeName = Type.FullName!;
            return string.IsNullOrEmpty(Name) ? typeName : string.Format("{0}.{1}", typeName, Name);
        }

        /// <summary>
        /// 获取对象的哈希值。
        /// </summary>
        /// <returns>对象的哈希值。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Name);
        }

        /// <summary>
        /// 比较对象是否与自身相等。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>被比较的对象是否与自身相等。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
        {
            return obj is TypeNamePair && Equals((TypeNamePair)obj);
        }

        /// <summary>
        /// 比较对象是否与自身相等。
        /// </summary>
        /// <param name="value">要比较的对象。</param>
        /// <returns>被比较的对象是否与自身相等。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TypeNamePair value)
        {
            return Type == value.Type && string.CompareOrdinal(Name, value.Name) == 0;
        }

        /// <summary>
        /// 判断两个对象是否相等。
        /// </summary>
        /// <param name="a">值 a。</param>
        /// <param name="b">值 b。</param>
        /// <returns>两个对象是否相等。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TypeNamePair a, TypeNamePair b)
        {
            return a.Equals(b);
        }

        /// <summary>
        /// 判断两个对象是否不相等。
        /// </summary>
        /// <param name="a">值 a。</param>
        /// <param name="b">值 b。</param>
        /// <returns>两个对象是否不相等。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TypeNamePair a, TypeNamePair b)
        {
            return !a.Equals(b);
        }
    }
}
