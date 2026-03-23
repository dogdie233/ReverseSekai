import os
import zlib
import msgpack
import json
from mitmproxy import http, ctx
from Crypto.Cipher import AES
from Crypto.Util.Padding import unpad

class TrafficDecryptor:
    def __init__(self):
        # 从环境变量读取 Key 和 IV
        self.key = bytes.fromhex(os.getenv("AES_KEY", ""))
        self.iv = bytes.fromhex(os.getenv("AES_IV", ""))
        
        self.target_hosts = [
            "mkcn-prod-public-60001-1.dailygn.com",
            "mkcn-prod-public-60001-2.dailygn.com"
        ]

    def load(self, loader):
        """
        当脚本加载时，自动配置 mitmproxy 的选项
        """
        # 只有匹配这两个域名的流量才进行 TLS 解密
        # 其他流量将直接进行 TCP 转发，不触碰证书，不解析内容
        allow_pattern = "|".join([host.replace(".", r"\.") for host in self.target_hosts])
        ctx.options.allow_hosts = [f"^{allow_pattern}$"]
        print(f"[*] 已设置 TLS 过滤，仅拦截: {self.target_hosts}")

    def decrypt_aes_msgpack(self, raw_data):
        """核心解密逻辑"""
        try:
            cipher = AES.new(self.key, AES.MODE_CBC, self.iv)
            decrypted = unpad(cipher.decrypt(raw_data), AES.block_size)
            # 尝试解压 (如果解密后是 gzip)
            try:
                decrypted = zlib.decompress(decrypted, 16 + zlib.MAX_WBITS)
            except:
                pass
            return msgpack.unpackb(decrypted, raw=False)
        except:
            return None

    def format_body(self, content, content_type):
        """根据 Content-Type 解析 Body"""
        if not content:
            return "<Empty Body>"
        
        ct = content_type.lower()

        # 1. JSON 处理
        if "application/json" in ct:
            try:
                data = json.loads(content.decode('utf-8', 'ignore'))
                return json.dumps(data, indent=4, ensure_ascii=False)
            except:
                return content.decode('utf-8', 'replace')

        # 2. 文本处理 (Text, Form-urlencoded, XML等)
        elif "text/" in ct or "application/x-www-form-urlencoded" in ct or "javascript" in ct:
            try:
                return content.decode('utf-8', 'replace')
            except:
                return content.hex(' ')

        # 3. 二进制流 (重点处理对象)
        elif "application/octet-stream" in ct:
            # 尝试 AES 解密
            result = self.decrypt_aes_msgpack(content)
            if result is not None:
                return f"[AES-Decrypted MsgPack]:\n{json.dumps(result, indent=4, ensure_ascii=False)}"
            else:
                # 解密失败，打印 Hex
                return f"[Binary Hex]:\n{content.hex(' ')}"

        # 4. 其他情况
        else:
            # 默认尝试打印前 100 字节的 Hex
            return f"[Unknown Content-Type: {content_type}]\nHex: {content[:200].hex(' ')} ..."

    def request(self, flow: http.HTTPFlow):
        if flow.request.pretty_host in self.target_hosts:
            ct = flow.request.headers.get("Content-Type", "")
            # 使用 raw_content 避免 mitmproxy 自动解压报错
            body_display = self.format_body(flow.request.raw_content, ct)
            self.print_log("REQUEST", flow, flow.request.headers, body_display)

    def response(self, flow: http.HTTPFlow):
        if flow.request.pretty_host in self.target_hosts:
            ct = flow.response.headers.get("Content-Type", "")
            body_display = self.format_body(flow.response.raw_content, ct)
            self.print_log("RESPONSE", flow, flow.response.headers, body_display)

    def print_log(self, direction, flow, headers, body_str):
        color = "\033[92m" if direction == "REQUEST" else "\033[94m"
        reset = "\033[0m"
        
        print(f"\n{color}{'='*40} {direction} {'='*40}{reset}")
        print(f"🌐 [URL]    : {flow.request.url}")
        print(f"📝 [METHOD] : {flow.request.method} | [STATUS]: {getattr(flow.response, 'status_code', 'N/A')}")
        print(f"🔖 [HEADERS]:")
        for k, v in headers.items():
            print(f"   {k}: {v}")
        print(f"\n📦 [BODY_DATA]:")
        print(body_str)
        print(f"{color}{'='*90}{reset}\n")

addons = [
    TrafficDecryptor()
]