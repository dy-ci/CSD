namespace CSD;

/// <summary>
/// 存储应用程序敏感配置信息的类
/// 注意：在实际生产环境中，这些值应该从安全存储（如环境变量、密钥管理服务）中获取
/// 
/// 使用说明：
/// 1. 复制此文件为 Secrets.cs
/// 2. 将 YOUR_APP_ID_HERE 替换为实际的应用程序 ID
/// 3. Secrets.cs 不会被提交到 Git 仓库（已在 .gitignore 中排除）
/// </summary>
public static class Secrets
{
    /// <summary>
    /// 应用程序ID，用于服务器认证
    /// </summary>
    public const string AppId = "YOUR_APP_ID_HERE";
}
