"""
解码 hook_wow64_log.txt 中的 W64 写入数据
"""
import struct
import re

LOG_PATH = r'E:\ceshi\6\hook_wow64_log.txt'

# 提取所有 W64 写入
pattern = re.compile(r'\[W64 other\] H=(0x[0-9a-f]+) Base=(0x[0-9a-f]+) Size=0x([0-9a-f]+) status=0x[0-9a-f]+ data=([0-9a-f ]+)')

writes = []
with open(LOG_PATH, 'r', encoding='utf-8') as f:
    for line in f:
        m = pattern.search(line)
        if m:
            h = m.group(1)
            base = int(m.group(2), 16)
            size = int(m.group(3), 16)
            data_hex = m.group(4).replace(' ', '')
            writes.append((h, base, size, data_hex))

print(f"共找到 {len(writes)} 次 W64 写入\n")

# 按句柄分组
from collections import OrderedDict
groups = OrderedDict()
for h, base, size, data in writes:
    if h not in groups:
        groups[h] = []
    groups[h].append((base, size, data))

print(f"句柄数: {len(groups)}\n")

# 解码每次传送（3个坐标一组）
print("=" * 80)
print("传送点解码（按句柄分组，每次3个写入=1次传送）")
print("=" * 80)

teleport_points = []
for h, items in groups.items():
    print(f"\n句柄 {h}: {len(items)} 次写入")
    # 每3个一组
    for i in range(0, len(items), 3):
        if i + 2 < len(items):
            b1, s1, d1 = items[i]
            b2, s2, d2 = items[i+1]
            b3, s3, d3 = items[i+2]
            # 解码为 double
            try:
                v1 = struct.unpack('<d', bytes.fromhex(d1[:16]))[0]
                v2 = struct.unpack('<d', bytes.fromhex(d2[:16]))[0]
                v3 = struct.unpack('<d', bytes.fromhex(d3[:16]))[0]
                print(f"  传送 #{i//3 + 1}:")
                print(f"    Base=0x{b1:016x} -> X = {v1:.4f}")
                print(f"    Base=0x{b2:016x} -> Z = {v2:.4f}")
                print(f"    Base=0x{b3:016x} -> Y = {v3:.4f}")
                teleport_points.append((v1, v2, v3))
            except Exception as e:
                print(f"  解码失败: {e}")

print("\n" + "=" * 80)
print(f"共 {len(teleport_points)} 个传送点")
print("=" * 80)
for i, (x, z, y) in enumerate(teleport_points, 1):
    print(f"  传送点 {i}: X={x:.4f}, Z={z:.4f}, Y={y:.4f}")

# 提取公共 base 偏移
print("\n" + "=" * 80)
print("地址分析")
print("=" * 80)
bases = set()
for h, base, size, data in writes:
    bases.add(base)
for b in sorted(bases):
    print(f"  Base = 0x{b:016x}")

# 推测对象基址
if bases:
    min_base = min(bases)
    print(f"\n  对象基址推测: 0x{min_base:016x}")
    print(f"  偏移:")
    for b in sorted(bases):
        print(f"    +0x{b - min_base:x} (0x{b:016x})")
