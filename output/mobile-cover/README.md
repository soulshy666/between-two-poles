# 手机封面适配验收

本次处理：手机横屏背景按比例铺满，必要时居中裁切；标题和菜单沿用安全区布局；恢复 CRT 曲面、扫描线、辉光，并同步修正曲面上的按钮点击坐标。

手机端先将封面绘制到独立纹理，再应用 CRT，不再使用桌面封面原有的 GrabPass。无法创建资源或 shader 不支持时回退直接绘制。未修改关卡、黑洞传送、地图、移动或存档数据逻辑。

## 已完成验证

- 编辑器手机适配集成检查通过（50 项）。截图在 integration 中。
- 封面专项检查通过（104 项）：16:9 / 20:9、1080p、设置和存档页面、绘制纹理释放后重建、曲面按钮坐标、CRT 开关。详情见 regression.txt。
- wide-cover.png 等截图为 Unity 编辑器 GPU 渲染结果，不是手机实测。

## 手机验收

新包路径：output/android/between-two-poles-mobile-crt.apk。
旧的可玩包保留在 output/android/between-two-poles-android-cover-check.apk。

1. 将新 APK 发到安卓手机，覆盖安装；不要先卸载或清除数据。
2. 完全退出后重新启动，确认封面不黑屏、画面方向正确，背景没有原来的宽黑边，CRT 效果正常。
3. 点设置、音量加减、返回，确认显示与点击位置一致。
4. 打开存档页，用已有存档或空槽开始游戏，确认能进入关卡。
5. 切到其他应用再返回，确认封面和按钮仍正常。

如果安装提示签名不一致，先停止安装并反馈提示，避免误删原有存档。
当前尚未完成安卓真机验收，也没有完成 iOS 构建或真机验证。

## 队友复现

用 Unity 2022.3.60f1c1 打开项目，安装 Android Build Support（包含 SDK、NDK、OpenJDK），运行菜单 Between Poles > Build > Android playtest APK。输出为 output/android/between-two-poles-test.apk。

提交代码时包含新增脚本、两个 shader 以及对应 .meta。APK 无需提交到 Git。封面专项检查工具仅在显式调用 BetweenPoles.CoverMobileRegression.Run 时运行，完成后会退出编辑器，使用前应保存正在编辑的内容。

本次未执行 Git 提交、推送或合并。
