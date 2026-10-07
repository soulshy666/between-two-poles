# U 型圆环组合路径碰撞回归 · 2026-10-07

路径采样已加入并排同向 U 型的反向 90° 平面转动。转动时保留间距，开口相对后直线靠拢，双方不绕行换位、不翻面。沿推动轴的必要翻面和其他原有路径同时回归。

256 paths sampled at 121 fixed times each; 30976 samples; mesh rectangle SAT checks, no extra rotation in aligned cases, straight direct docking, only misaligned axial halves flip; unchanged halves retain pose; flip-stage bounds separated; continuous floor contact; final mesh seam depth=0.03157, maximum extra penetration=0.00000; Failures=0

碰撞检查：翻面阶段检查实际模型整体包围盒分离，闭合阶段检查模型分段矩形投影。现有最终圆环两端接缝约有 0.03157 单位搭接；全路径不得新增超出最终接缝的相交。
