namespace DataTables
{
    /// <summary>
    /// 数据表状态枚举
    /// </summary>
    public enum DataTableStatus
    {
        /// <summary>
        /// 未加载
        /// </summary>
        NotLoaded,

        /// <summary>
        /// 已加载但为空
        /// </summary>
        LoadedEmpty,

        /// <summary>
        /// 已加载且包含数据
        /// </summary>
        LoadedWithData
    }

    /// <summary>
    /// 数据表加载状态枚举
    /// </summary>
    public enum DataTableLoadStatus
    {
        /// <summary>
        /// 未加载
        /// </summary>
        NotLoaded,

        /// <summary>
        /// 加载中
        /// </summary>
        Loading,

        /// <summary>
        /// 已加载但为空
        /// </summary>
        LoadedEmpty,

        /// <summary>
        /// 准备就绪（已加载且包含数据）
        /// </summary>
        Ready,

        /// <summary>
        /// 加载失败
        /// </summary>
        Failed
    }
}