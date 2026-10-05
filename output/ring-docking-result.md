# U 型沿推动轴翻面对接回归 · 2026-10-06

只翻开口背向对方的材料：双方背对背则双方翻面；推动方背向、接收方已经朝向推动方，则仅推动方翻面。材料保持原来两侧，接收方只在最后靠拢时稍后移。

512 ring cases (4 receiver orientations x 4 incoming orientations x 4 push directions x 2 colors x 4 face pairs); axial pairs flip only misaligned halves and retain sides; other pairs use minimum planar turn; continuous floor contact, color halves, model-swap continuity, player landing, undo/replay; 39 frames; Failures=0

碰撞检查：翻面阶段检查实际模型整体包围盒分离，闭合阶段检查模型分段矩形投影。现有最终圆环两端接缝约有 0.03157 单位搭接；全路径不得新增超出最终接缝的相交。
