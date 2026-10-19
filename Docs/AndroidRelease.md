# Android 正式签名包

正式包名：`com.betweentwopoles.game`。显示名称：两极之间。
首版：`0.1.0`，VersionCode：`1`。与 `com.betweentwopoles.playtest` 测试包并存，存档不会自动互通。

## 打包

使用 Unity 2022.3.60f1c1，安装 Android Build Support（SDK、NDK、OpenJDK）。
菜单选择 **Between Poles > Build > Android signed release APK**。
输出：`output/android/between-two-poles-release-0.1.0.apk`。
此入口使用 IL2CPP、ARMv7 + ARM64，关闭 Development、脚本调试和自动连接 Profiler。
构建结束恢复原有项目配置，不把签名密码保存在项目设置中。测试包入口仍然独立保留。

## 签名保管

本机签名资料位于 `%LOCALAPPDATA%\BetweenTwoPoles\AndroidSigning`，不在 Git 仓库中：

- `between-two-poles-release.keystore`：以后所有正式版本必须沿用的签名密钥。
- `signing.json`：本机打包配置，含密码，按敏感文件保管，不能提交 Git 或公开发送。

请把**整个目录**另行备份到团队控制的安全位置，例如加密移动存储或受限密码库；只保存在本机不是备份。
已有正式发布后，不可为了换电脑或忘记密码而重新生成另一把密钥。

其他打包电脑通过安全方式取得同一份密钥和配置后，放在该用户对应的目录中，并将 `signing.json` 的 `keystorePath` 更新为实际绝对路径。
也可设置环境变量 `BETWEEN_POLES_SIGNING_CONFIG` 指向仓库外的配置文件。JSON 字段为 `keystorePath`、`keystorePassword`、`alias`、`aliasPassword`。
打包脚本缺少配置时直接报错，不会自动创建或替换密钥。

## 后续更新

在 `Assets/BetweenPoles/Editor/AndroidReleaseBuild.cs` 更新 `VersionName` 和 `VersionCode`。每次上传新版本都应提高 VersionCode；包名和签名保持一致。
重新打包后，在真机检查冷启动、封面、设置、进入关卡，以及从上一正式版本覆盖安装后存档是否保留，再上传平台。
正式签名不代表平台已经审核通过，也不代表完成了 iOS 签名和构建。
