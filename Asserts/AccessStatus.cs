namespace AutoPatrol.Asserts
{
    public enum AccessStatus
    {
        /// <summary>
        /// 访问成功
        /// </summary>
        Success,

        /// <summary>
        /// 用户名或密码错误
        /// </summary>
        InvalidCredentials,

        /// <summary>
        /// 共享路径不存在
        /// </summary>
        PathNotFound,

        /// <summary>
        /// 根路径不存在
        /// </summary>
        RootPathNotFound,

        /// <summary>
        /// 没有访问权限
        /// </summary>
        PermissionDenied,

        /// <summary>
        /// 网络异常
        /// </summary>
        NetworkError,

        /// <summary>
        /// 未知错误
        /// </summary>
        UnknownError
    }
}
