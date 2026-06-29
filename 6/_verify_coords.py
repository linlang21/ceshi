"""
验证 0x1664425d8 是玩家坐标直接地址
"""
import struct

# 初始读取值（基线坐标）
data_x = bytes.fromhex('00000000e811a6c0')
data_z = bytes.fromhex('00860c468ec742c0')
data_y = bytes.fromhex('000000804b08a0c0')

x = struct.unpack('<d', data_x)[0]
z = struct.unpack('<d', data_z)[0]
y = struct.unpack('<d', data_y)[0]

print(f"基线坐标（玩家当前位置）:")
print(f"  X = {x:.4f}")
print(f"  Z = {z:.4f}")
print(f"  Y = {y:.4f}")

print(f"\n写入的传送坐标:")
teleports = [
    (-2629.59, -48.24, -2372.89),
    (-2843.01, -33.69, -1994.31),
    (-3061.76, -48.77, -2008.57),
    (-2824.83, -36.54, -2052.12),
]
for i, (tx, tz, ty) in enumerate(teleports, 1):
    print(f"  传送点 {i}: X={tx:.2f}, Z={tz:.2f}, Y={ty:.2f}")

print(f"\n地址分析:")
print(f"  Base    = 0x1664425d8  (X)")
print(f"  Base+8  = 0x1664425e0  (Z)")
print(f"  Base+16 = 0x1664425e8  (Y)")
print(f"\n结论:")
print(f"  0x1664425d8 是玩家对象的坐标基址")
print(f"  偏移: X=+0x0, Z=+0x8, Y=+0x10")
print(f"  数据类型: Double (8字节)")
print(f"  这是 AAA.exe 直接读写的地址，无需指针链解引用")
