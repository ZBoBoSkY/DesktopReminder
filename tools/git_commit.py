# -*- coding: utf-8 -*-
"""git 提交辅助脚本（dulwich 实现）。

用途：本机安全软件按进程拦截 git.exe 的本地写入，本脚本用纯 Python 的
dulwich 完成等价的 git 操作（暂存/提交/打标签），行为与 git 一致。

用法：
    python git_commit.py "提交信息"
    python git_commit.py "提交信息" v0.3.0      # 提交并打标签
"""
import os
import sys

from dulwich import porcelain
from dulwich.repo import Repo

PROJECT = os.path.dirname(os.path.abspath(__file__))
AUTHOR = b"zbobosky <jojer@163.com>"


def main() -> None:
    if len(sys.argv) < 2:
        print("用法: python git_commit.py \"提交信息\" [标签名]")
        sys.exit(1)

    message = sys.argv[1]
    tag = sys.argv[2] if len(sys.argv) > 2 else None

    repo = Repo(PROJECT)
    porcelain.add(repo, ".")
    commit_id = porcelain.commit(
        repo,
        message=message.encode("utf-8"),
        author=AUTHOR,
        committer=AUTHOR,
    )
    short = commit_id.decode("ascii")[:12]
    print(f"已提交: {short} - {message}")

    if tag:
        repo.refs[b"refs/tags/" + tag.encode("utf-8")] = commit_id
        print(f"已打标签: {tag} -> {short}")


if __name__ == "__main__":
    main()
