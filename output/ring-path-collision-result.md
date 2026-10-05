# U 型沿推动轴翻面对接回归 · 2026-10-06

只翻开口背向对方的材料：双方背对背则双方翻面；推动方背向、接收方已经朝向推动方，则仅推动方翻面。材料保持原来两侧，接收方只在最后靠拢时稍后移。

256 paths sampled at 121 fixed times each; 30976 samples; mesh rectangle SAT checks, no extra rotation in aligned cases, straight direct docking, only misaligned axial halves flip; unchanged halves retain pose; flip-stage bounds separated; continuous floor contact; final mesh seam depth=0.03157, maximum extra penetration=0.00000; Failures=0

碰撞检查：翻面阶段检查实际模型整体包围盒分离，闭合阶段检查模型分段矩形投影。现有最终圆环两端接缝约有 0.03157 单位搭接；全路径不得新增超出最终接缝的相交。
