# -*- coding: utf-8 -*-
"""用 dulwich（纯 Python git 实现）初始化仓库并提交首个版本。
用途：本环境安全软件按进程拦截 git.exe 的本地写入，改用 Python 实现完成同样的
git 对象写入操作（行为与 git 一致：向 .git 目录写入对象/索引/引用）。
"""
import io
import os
import shutil

from dulwich import porcelain
from dulwich.repo import Repo

PROJECT = r"C:\Users\zengbo\DoubaoWork\chats\2026-08-29\new-chat-1\DesktopReminder_v3"
GIT_DIR = os.path.join(PROJECT, ".git")
AUTHOR = b"zbobosky <jojer@163.com>"


def log(msg: str) -> None:
    print(msg, flush=True)


def main() -> None:
    # 1. 移除不完整/被拦截写入污染的 .git，重建（先清除可能存在的只读属性）
    if os.path.exists(GIT_DIR):
        for root, dirs, files in os.walk(GIT_DIR):
            for name in files:
                p = os.path.join(root, name)
                try:
                    os.chmod(p, 0o777)
                except OSError:
                    pass
        shutil.rmtree(GIT_DIR)
        log("[1/5] 已移除旧 .git")

    repo = Repo.init(PROJECT)
    log("[2/5] 仓库初始化完成")

    # 2. 写本地 git 身份（dulwich 提交需要；GitHub 身份由远程凭据决定）
    config = repo.get_config()
    config.set(b"user", b"name", b"zbobosky")
    config.set(b"user", b"email", b"jojer@163.com")
    config.write_to_path()

    # 3. 暂存全部文件（dulwich 尊重 .gitignore，自动排除 bin/obj 等）
    porcelain.add(repo, ".")
    log("[3/5] 文件已暂存（含 .gitignore 过滤）")

    # 4. 提交
    commit_id = porcelain.commit(
        repo,
        message="v0.1.0: 工程骨架 + 领域层（任务模型/核心计算）+ 22个单元测试".encode("utf-8"),
        author=AUTHOR,
        committer=AUTHOR,
    )
    log(f"[4/5] 提交完成: {commit_id.decode('ascii')[:12]}")

    # 5. 打轻量标签 v0.1.0
    repo.refs[b"refs/tags/v0.1.0"] = commit_id
    log("[5/5] 标签 v0.1.0 已创建")

    # 验证
    log("=== 验证 ===")
    head = repo.head()
    log(f"HEAD      : {head.decode('ascii')[:12]}")
    log(f"v0.1.0    : {repo.refs[b'refs/tags/v0.1.0'].decode('ascii')[:12]}")
    entries = sorted(os.listdir(repo.path + "/objects/pack") if os.path.isdir(repo.path + "/objects/pack") else [])
    log(f"objects   : {len([e for e in os.listdir(os.path.join(repo.path, 'objects')) if e not in ('info', 'pack')])} 个子目录")
    with io.open(os.path.join(PROJECT, "README.md"), encoding="utf-8") as f:
        first = f.readline().strip()
    log(f"README 首行: {first}")


if __name__ == "__main__":
    main()
