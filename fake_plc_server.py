#!/usr/bin/env python3
"""
SLMP 3E Frame 가상 PLC 서버
Unity SmartHandWash 프로젝트와 실제 TCP 통신 테스트용

사용법:
    python fake_plc_server.py

Unity 설정:
    PLCConfig.json에서 useMock=false, ip="127.0.0.1" 로 변경
"""

import socket
import struct

HOST = '127.0.0.1'
PORT = 5007

# 가상 PLC 메모리
D = {0: 1000, 10: 0}   # D0=비누잔량(1000=100%), D10=사용횟수
M = {0: 0, 1: 0, 2: 0, 10: 0, 11: 0}


def make_response(data_bytes):
    """SLMP 3E Frame 응답 래핑"""
    data_len = len(data_bytes) + 2   # 종료코드 포함
    header = bytes([
        0xD0, 0x00,              # 서브헤더 (응답)
        0x00,                    # 네트워크 번호
        0xFF,                    # PC 번호
        0xFF, 0x03,              # 응답처 I/O
        0x00,                    # 스테이션
        data_len & 0xFF,
        (data_len >> 8) & 0xFF,
        0x00, 0x00,              # 종료코드 (정상)
    ])
    return header + data_bytes


def handle_read_word(dev_num, count):
    """워드 읽기 응답"""
    data = b''
    for i in range(count):
        val = D.get(dev_num + i, 0)
        data += struct.pack('<H', val)
    return make_response(data)


def handle_read_bit(dev_num, count):
    """비트 읽기 응답 (니블 패킹)"""
    data = b''
    for i in range(0, count, 2):
        b0 = 0x10 if M.get(dev_num + i, 0) else 0x00
        b1 = 0x01 if M.get(dev_num + i + 1, 0) else 0x00
        data += bytes([b0 | b1])
    return make_response(data)


def handle_write_word(dev_num, count, data_bytes):
    """워드 쓰기 처리"""
    for i in range(count):
        val = struct.unpack_from('<H', data_bytes, i * 2)[0]
        D[dev_num + i] = val
        print(f"    D{dev_num + i} = {val}")
    return make_response(b'')


def handle_write_bit(dev_num, count, data_bytes):
    """비트 쓰기 처리"""
    for i in range(count):
        byte_idx = i // 2
        if i % 2 == 0:
            val = (data_bytes[byte_idx] & 0x10) != 0
        else:
            val = (data_bytes[byte_idx] & 0x01) != 0
        M[dev_num + i] = 1 if val else 0
        print(f"    M{dev_num + i} = {val}")
    return make_response(b'')


def simulate_soap_use():
    """비누 사용 시뮬레이션 (서버 콘솔에서 's' 입력 시 호출)"""
    D[0] = max(0, D[0] - 50)
    D[10] += 1
    if D[0] <= 200:
        M[10] = 1  # 비누 알람
    print(f"[시뮬레이션] 비누 사용 → D0={D[0]}, D10={D[10]}, M10={M[10]}")


def main():
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind((HOST, PORT))
    server.listen(1)
    print(f"=" * 50)
    print(f"  SLMP 가상 PLC 서버 시작")
    print(f"  주소: {HOST}:{PORT}")
    print(f"=" * 50)
    print(f"초기 메모리:")
    print(f"  D0 (비누 잔량) = {D[0]} (100%)")
    print(f"  D10 (사용 횟수) = {D[10]}")
    print(f"")
    print(f"Unity 연결 대기 중...")
    print(f"")

    while True:
        conn, addr = server.accept()
        print(f"[연결됨] {addr}")
        try:
            while True:
                data = conn.recv(1024)
                if not data:
                    break

                # 명령어 파싱 (바이트 11~12: 명령, 13~14: 서브명령)
                if len(data) < 21:
                    continue

                cmd = data[11] | (data[12] << 8)
                sub_cmd = data[13] | (data[14] << 8)
                dev_num = data[15] | (data[16] << 8) | (data[17] << 16)
                count = data[19] | (data[20] << 8)

                if cmd == 0x0401 and sub_cmd == 0x0000:
                    # 워드 읽기
                    resp = handle_read_word(dev_num, count)
                    values = [D.get(dev_num + i, 0) for i in range(count)]
                    print(f"  [읽기] D{dev_num} x{count} → {values}")

                elif cmd == 0x0401 and sub_cmd == 0x0001:
                    # 비트 읽기
                    resp = handle_read_bit(dev_num, count)
                    values = [M.get(dev_num + i, 0) for i in range(count)]
                    print(f"  [읽기] M{dev_num} x{count} → {values}")

                elif cmd == 0x1401 and sub_cmd == 0x0000:
                    # 워드 쓰기
                    print(f"  [쓰기] D{dev_num} x{count}")
                    resp = handle_write_word(dev_num, count, data[21:])

                elif cmd == 0x1401 and sub_cmd == 0x0001:
                    # 비트 쓰기
                    print(f"  [쓰기] M{dev_num} x{count}")
                    resp = handle_write_bit(dev_num, count, data[21:])

                else:
                    print(f"  [알 수 없는 명령] cmd=0x{cmd:04X}, sub=0x{sub_cmd:04X}")
                    resp = make_response(b'')

                conn.send(resp)

        except Exception as e:
            print(f"[오류] {e}")
        finally:
            conn.close()
            print(f"[연결 종료] 클라이언트 연결 해제")
            print(f"Unity 재연결 대기 중...")


if __name__ == '__main__':
    main()
