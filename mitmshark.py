import os
import zlib
import msgpack
import json
import re
from datetime import datetime
from mitmproxy import http, ctx
from Crypto.Cipher import AES
from Crypto.Util.Padding import unpad

class TrafficDecryptor:
    def __init__(self):
        # 从环境变量读取 Key 和 IV
        self.key = bytes.fromhex(os.getenv("AES_KEY", ""))
        self.iv = bytes.fromhex(os.getenv("AES_IV", ""))

        # configurable options
        self.save_logs = False  # 是否保存日志到文件
        self.intercept_enabled = True
        self.intercept_scheme = "http"
        self.intercept_host = "127.0.0.1"
        self.intercept_port = 5000
        
        self.api_hosts = [
            "production-game-api.sekai.colorfulpalette.org"
        ]
        self.log_hosts = [
            "production-game-api.sekai.colorfulpalette.org",
            "game-version.sekai.colorfulpalette.org",
            # "production-cf2d2388-assetbundle.sekai.colorfulpalette.net",
            "production-cf2d2388-assetbundle-info.sekai.colorfulpalette.org"
        ]
        
        self.target_hosts = set(self.api_hosts + self.log_hosts)
    
        if not self.key or not self.iv:
            print("[!] 请确保环境变量 AES_KEY 和 AES_IV 已正确设置为 16 字节的十六进制字符串")
            exit(1)
        if self.intercept_enabled:
            print(f"[*] 已启用流量拦截，目标域名将被重定向到 {self.intercept_host}:{self.intercept_port}")

        self.log_dir = "traffic_logs"
        if not os.path.exists(self.log_dir):
            os.makedirs(self.log_dir)

    def load(self, loader):
        allow_pattern = "|".join([host.replace(".", r"\.") for host in self.target_hosts])
        ctx.options.allow_hosts = [f"^{allow_pattern}$"]
        print(f"[*] 已设置 TLS 过滤，仅拦截: {self.target_hosts}")

    def decrypt_aes_msgpack(self, raw_data):
        try:
            cipher = AES.new(self.key, AES.MODE_CBC, self.iv)
            decrypted = unpad(cipher.decrypt(raw_data), AES.block_size)
            try:
                decrypted = zlib.decompress(decrypted, 16 + zlib.MAX_WBITS)
            except:
                pass
            return msgpack.unpackb(decrypted, raw=False)
        except:
            return None

    def format_body(self, content, content_type):
        if not content:
            return "<Empty Body>"
        
        ct = content_type.lower()
        if "application/json" in ct:
            try:
                data = json.loads(content.decode('utf-8', 'ignore'))
                return json.dumps(data, indent=4, ensure_ascii=False)
            except:
                return content.decode('utf-8', 'replace')
        elif "text/" in ct or "application/x-www-form-urlencoded" in ct or "javascript" in ct:
            try:
                return content.decode('utf-8', 'replace')
            except:
                return content.hex(' ')
        elif "application/octet-stream" in ct:
            result = self.decrypt_aes_msgpack(content)
            if result is not None:
                return f"[AES-Decrypted MsgPack]:\n{json.dumps(result, indent=4, ensure_ascii=False)}"
            else:
                return f"[Binary Hex]:\n{content.hex(' ')}"
        else:
            return f"[Unknown Content-Type: {content_type}]\nHex: {content[:200].hex(' ')} ..."

    def request(self, flow: http.HTTPFlow):
        # 如果开启了拦截且是目标域名，重定向流量到本地服务器
        if self.intercept_enabled and flow.request.pretty_host in self.api_hosts:
            flow.request.headers["Nya-Original-Host"] = flow.request.pretty_host
            flow.request.scheme = self.intercept_scheme
            flow.request.host = self.intercept_host
            flow.request.port = self.intercept_port

    def response(self, flow: http.HTTPFlow):
        if flow.request.pretty_host in self.target_hosts or flow.request.host == self.intercept_host:
            # 1. 格式化请求内容
            req_ct = flow.request.headers.get("Content-Type", "")
            req_body = self.format_body(flow.request.raw_content, req_ct)
            
            # 2. 格式化响应内容
            res_ct = flow.response.headers.get("Content-Type", "")
            res_body = self.format_body(flow.response.raw_content, res_ct)

            # 3. 生成展示用的文本（带颜色）
            log_content = self.generate_combined_log(flow, req_body, res_body)
            
            # 4. 打印到控制台
            print(log_content)

            # 5. 保存到文件（去除颜色）
            self.save_log_to_file(flow, log_content)

    def generate_combined_log(self, flow, req_body, res_body):
        """生成格式化的请求响应对文本"""
        c_req = "\033[92m" # Green
        c_res = "\033[94m" # Blue
        c_rst = "\033[0m"
        
        lines = []
        lines.append(f"\n{c_req}{'='*30} REQUEST {'='*30}{c_rst}")
        lines.append(f"🌐 [URL]    : {flow.request.url}")
        lines.append(f"📝 [METHOD] : {flow.request.method}")
        lines.append(f"🔖 [HEADERS]:")
        for k, v in flow.request.headers.items():
            lines.append(f"   {k}: {v}")
        lines.append(f"\n📦 [BODY_DATA]:\n{req_body}")

        lines.append(f"\n{c_res}{'='*30} RESPONSE (Status: {flow.response.status_code}) {'='*30}{c_rst}")
        lines.append(f"🔖 [HEADERS]:")
        for k, v in flow.response.headers.items():
            lines.append(f"   {k}: {v}")
        lines.append(f"\n📦 [BODY_DATA]:\n{res_body}")
        lines.append(f"{'='*70}\n")
        
        return "\n".join(lines)

    def save_log_to_file(self, flow, content):
        if self.save_logs is False:
            return
        """将内容存入文件"""
        # 移除 ANSI 颜色代码
        clean_text = re.sub(r'\x1b\[[0-9;]*[mGKF]', '', content)
        
        # 生成文件名: 时间_路径.txt (处理掉非法字符)
        timestamp = datetime.now().strftime("%H%M%S_%f")[:-3]
        path_name = flow.request.path.split('?')[0].replace('/', '_')[-50:] # 取路径后50位
        filename = f"{timestamp}{path_name}.txt"
        
        filepath = os.path.join(self.log_dir, filename)
        
        try:
            with open(filepath, "w", encoding="utf-8") as f:
                f.write(f"Timestamp: {datetime.now().isoformat()}\n")
                f.write(clean_text)
        except Exception as e:
            print(f"[!] 写入文件失败: {e}")

addons = [
    TrafficDecryptor()
]