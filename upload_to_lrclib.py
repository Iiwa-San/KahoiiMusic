import urllib.request, json, hashlib, sys, os

def solve_challenge():
    print("[1/3] Đang lấy mã bảo mật từ LRCLIB...")
    req = urllib.request.Request(
        'https://lrclib.net/api/request-challenge',
        headers={'User-Agent': 'KahoiiMusic/2.0'},
        data=b'',
        method='POST'
    )
    resp = urllib.request.urlopen(req)
    data = json.loads(resp.read().decode('utf-8'))
    prefix = data['prefix']
    target_hex = data['target']
    target = bytes.fromhex(target_hex)

    print("[2/3] Đang giải mã Proof-of-Work...")
    nonce = 0
    while True:
        input_str = f"{prefix}{nonce}".encode('utf-8')
        h = hashlib.sha256(input_str).digest()
        if h <= target:
            break
        nonce += 1
    
    return f"{prefix}:{nonce}"

def upload_lyrics(track_name, artist_name, album_name, duration_sec, lrc_content, plain_content=None):
    token = solve_challenge()
    print(f"[3/3] Đang tải lời bài hát lên LRCLIB (Token: {token[:12]}...)...")

    # If plain_content not provided, strip timestamps from lrc
    if not plain_content and lrc_content:
        import re
        lines = []
        for line in lrc_content.splitlines():
            cleaned = re.sub(r'\[\d{2}:\d{2}(?:\.\d{2,3})?\]', '', line).strip()
            if cleaned:
                lines.append(cleaned)
        plain_content = "\n".join(lines)

    payload = {
        "trackName": track_name.strip(),
        "artistName": artist_name.strip(),
        "albumName": (album_name or track_name).strip(),
        "duration": float(duration_sec),
        "plainLyrics": plain_content,
        "syncedLyrics": lrc_content
    }

    req = urllib.request.Request(
        'https://lrclib.net/api/publish',
        headers={
            'Content-Type': 'application/json',
            'User-Agent': 'KahoiiMusic/2.0 (https://github.com/kahoii/music)',
            'X-Publish-Token': token
        },
        data=json.dumps(payload, ensure_ascii=False).encode('utf-8'),
        method='POST'
    )

    try:
        resp = urllib.request.urlopen(req)
        print(f"✓ THÀNH CÔNG! Đã đóng góp lời bài hát '{track_name}' lên LRCLIB.")
        print("Bây giờ bạn có thể mở Kahoii Music và thưởng thức lời bài hát đồng bộ thời gian thực!")
        return True
    except urllib.error.HTTPError as e:
        err = e.read().decode('utf-8')
        print(f"✗ Lỗi tải lên ({e.code}): {err}")
        return False

if __name__ == '__main__':
    if len(sys.argv) < 5:
        print("Cách dùng:")
        print("  python upload_to_lrclib.py <tên_bài> <ca_sĩ> <thời_lượng_giây> <đường_dẫn_file_lrc> [tên_album]")
        print("Ví dụ:")
        print("  python upload_to_lrclib.py \"From the Heart, Dare the Heavens\" \"Wuthering Waves\" 222 \"From_the_Heart.lrc\"")
        sys.exit(1)

    track = sys.argv[1]
    artist = sys.argv[2]
    dur = float(sys.argv[3])
    lrc_file = sys.argv[4]
    album = sys.argv[5] if len(sys.argv) > 5 else track

    if not os.path.exists(lrc_file):
        print(f"Không tìm thấy file: {lrc_file}")
        sys.exit(1)

    with open(lrc_file, 'r', encoding='utf-8') as f:
        lrc = f.read()

    upload_lyrics(track, artist, album, dur, lrc)

