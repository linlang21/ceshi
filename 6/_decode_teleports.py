"""Decode all W64 (WriteVirtualMemory64) calls from hook_wow64_log.txt
and document the full pointer chain extracted from AAA.exe."""
import struct
import re
from collections import OrderedDict

LOG_PATH = r'E:\ceshi\6\hook_wow64_log.txt'
YYSLS_BASE = 0x140000000

# Parse W64 lines
# Format: [W64 other] H=0x... Base=0x... Size=0x... status=0x... data=... ...
write_re = re.compile(
    r'\[W64 other\]\s+H=0x([0-9a-f]+)\s+Base=0x([0-9a-f]+)\s+Size=0x([0-9a-f]+)\s+status=0x([0-9a-f]+)\s+data=([0-9a-f ]+)'
)

teleports = OrderedDict()  # key: (X_bytes, Z_bytes, Y_bytes) -> count
writes_by_handle = OrderedDict()  # handle -> list of (addr, data_bytes)

with open(LOG_PATH, 'r', encoding='utf-8') as f:
    for line in f:
        m = write_re.search(line)
        if not m:
            continue
        h = int(m.group(1), 16)
        base = int(m.group(2), 16)
        size = int(m.group(3), 16)
        status = int(m.group(4), 16)
        data_hex = m.group(5).strip().replace(' ', '')
        data_bytes = bytes.fromhex(data_hex)

        writes_by_handle.setdefault(h, []).append((base, data_bytes))

print(f"=== Total handles used for writes: {len(writes_by_handle)} ===")
for h, writes in writes_by_handle.items():
    print(f"\nHandle 0x{h:x} — {len(writes)} writes:")
    for addr, data in writes:
        # Group by object offset
        obj_base = addr - 0x340  # try X offset
        if obj_base & 0x7:  # not aligned
            obj_base = addr - 0x348
            off = 0x348
        else:
            off = addr - obj_base
        if len(data) == 8:
            val = struct.unpack('<d', data)[0]
            print(f"  0x{addr:016X} (obj+0x{off:X}) = {val:.4f}  (bytes={data.hex()})")
        else:
            print(f"  0x{addr:016X} = {data.hex()}")

# Group writes into teleport events (3 consecutive writes = 1 teleport)
print("\n=== Teleport destinations (grouped by 3 consecutive writes) ===")
all_writes = []
for h, writes in writes_by_handle.items():
    for addr, data in writes:
        all_writes.append((h, addr, data))

# Group consecutive writes by handle (same handle = same teleport session)
i = 0
teleport_count = 0
while i < len(all_writes):
    h, addr, data = all_writes[i]
    # Find next 2 writes from same handle
    triple = [(addr, data)]
    j = i + 1
    while j < len(all_writes) and len(triple) < 3:
        if all_writes[j][0] == h:
            triple.append((all_writes[j][1], all_writes[j][2]))
        j += 1
    if len(triple) == 3:
        teleport_count += 1
        # Sort by address (X=0x340 < Z=0x348 < Y=0x350)
        triple.sort(key=lambda x: x[0])
        x_val = struct.unpack('<d', triple[0][1])[0]
        z_val = struct.unpack('<d', triple[1][1])[0]
        y_val = struct.unpack('<d', triple[2][1])[0]
        obj_base = triple[0][0] - 0x340
        print(f"\nTeleport #{teleport_count} (handle=0x{h:x}, obj=0x{obj_base:016X}):")
        print(f"  X (Double @+0x340) = {x_val:.4f}")
        print(f"  Z (Double @+0x348) = {z_val:.4f}")
        print(f"  Y (Double @+0x350) = {y_val:.4f}")
    i = j

print(f"\n=== Total teleports detected: {teleport_count} ===")

print("\n=== FULL POINTER CHAIN (extracted from AAA.exe reads) ===")
print(f"yysls.exe base = 0x{YYSLS_BASE:016X}")
print(f"Static address: yysls.exe + 0x083F46D8 = 0x{YYSLS_BASE + 0x083F46D8:016X}")
print(f"  Step 1: [yysls.exe + 0x083F46D8]   -> read 8-byte pointer (P1)")
print(f"  Step 2: [P1 + 0x58]                -> read 8-byte pointer (P2)")
print(f"  Step 3: [P2 + 0x00]                -> read 8-byte pointer (OBJ)  // player/object base")
print(f"  Step 4: [OBJ + 0x340] = X coordinate (Double, 8 bytes)")
print(f"  Step 5: [OBJ + 0x348] = Z coordinate (Double, 8 bytes)")
print(f"  Step 6: [OBJ + 0x350] = Y coordinate (Double, 8 bytes)")
print(f"\nAAA.exe uses NtWow64WriteVirtualMemory64 to write 8-byte Double values")
print(f"to OBJ+0x340 / +0x348 / +0x350 for teleportation.")
