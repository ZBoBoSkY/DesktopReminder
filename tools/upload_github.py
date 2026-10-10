# -*- coding: utf-8 -*-
"""通过 GitHub REST API (api.github.com) 推送本地仓库 —— 绕过被墙的 github.com git 通道。

用法:
    python upload_github.py <GITHUB_TOKEN>
    # 或设置环境变量 GITHUB_TOKEN 后: python upload_github.py

原理:
    1. 用 dulwich 读本地 git 对象（blob / tree / commit）
    2. 按依赖顺序上传到 GitHub Git Data API:
       blobs → trees → commits（从最早的无父提交开始）→ 更新 refs/heads/main（force）
    3. 全部走 api.github.com（国内可直连），不依赖 github.com 的 git 协议
"""
import base64
import json
import os
import sys
import urllib.request
import urllib.error

from dulwich.repo import Repo

OWNER = "ZBoBoSkY"
REPO = "DesktopReminder"
BRANCH = "refs/heads/main"
API = f"https://api.github.com/repos/{OWNER}/{REPO}/git"


def api(method: str, path: str, data: dict | None = None) -> dict:
    req = urllib.request.Request(API + path, method=method)
    req.add_header("Authorization", f"token {TOKEN}")
    req.add_header("Accept", "application/vnd.github+json")
    body = json.dumps(data).encode("utf-8") if data is not None else None
    if body is not None:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, body, timeout=30) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", "replace")
        raise RuntimeError(f"API {method} {path} 失败 {e.code}: {detail}") from e


def upload_blob(content: bytes) -> str:
    result = api("POST", "/blobs", {"content": base64.b64encode(content).decode("ascii"), "encoding": "base64"})
    return result["sha"]


def upload_tree(entries: list[dict]) -> str:
    result = api("POST", "/trees", {"tree": entries})
    return result["sha"]


def upload_commit(message: str, tree_sha: str, parents: list[str]) -> str:
    result = api("POST", "/commits", {
        "message": message,
        "tree": tree_sha,
        "parents": parents,
        "author": {"name": "zbobosky", "email": "jojer@163.com", "date": ""},
        "committer": {"name": "zbobosky", "email": "jojer@163.com", "date": ""},
    })
    return result["sha"]


def main() -> None:
    if not TOKEN:
        print("缺少 GITHUB_TOKEN，用法: python upload_github.py <GITHUB_TOKEN>")
        sys.exit(1)

    repo = Repo(os.path.dirname(os.path.abspath(__file__)))
    head = repo.head().decode("ascii")
    print(f"本地 HEAD: {head}")

    # 1) 收集需要上传的对象：从 HEAD 沿 parents 回溯所有 commit
    commits_to_upload: list[str] = []  # DFS 后序：父严格先于子
    seen: set[str] = set()

    def dfs(cid: str) -> None:
        if cid in seen:
            return
        seen.add(cid)
        for parent in repo[bytes.fromhex(cid)].parents:
            dfs(parent.decode("ascii"))
        commits_to_upload.append(cid)

    dfs(head)
    print(f"将上传 {len(commits_to_upload)} 个提交: {', '.join(c[:8] for c in commits_to_upload)}")

    # 2) 递归上传 blob / tree（带缓存）
    blob_cache: dict[str, str] = {}   # 本地对象id -> GitHub sha
    tree_cache: dict[str, str] = {}

    def upload_object(obj_id: bytes) -> str:
        oid = obj_id.decode("ascii")
        obj = repo[obj_id]
        if obj.type_name == b"blob":
            if oid not in blob_cache:
                blob_cache[oid] = upload_blob(obj.data)
            return blob_cache[oid]
        if obj.type_name == b"tree":
            if oid in tree_cache:
                return tree_cache[oid]
            entries = []
            for item in obj.iteritems():
                mode, name = item[0], item[1].decode("utf-8", "replace")
                child = item[2]  # sha bytes
                if mode == b"40000" or mode == b"040000":
                    entries.append({"path": name, "mode": "040000", "type": "tree",
                                    "sha": upload_object(child)})
                else:
                    entries.append({"path": name, "mode": mode.decode("ascii"), "type": "blob",
                                    "sha": upload_object(child)})
            tree_cache[oid] = upload_tree(entries)
            return tree_cache[oid]
        raise RuntimeError(f"未知对象类型: {obj.type_name}")

    # 3) 按拓扑序逐个创建 commit（父必须先于子存在于远程）
    created: dict[str, str] = {}
    for cid in commits_to_upload:
        commit = repo[bytes.fromhex(cid)]
        tree_sha = upload_object(commit.tree)
        parents = [created[p.decode("ascii")] for p in commit.parents]
        message = commit.message.decode("utf-8", "replace").rstrip("\n")
        created[cid] = upload_commit(message, tree_sha, parents)
        print(f"  上传提交 {cid[:8]} -> {created[cid][:8]}  ({message[:48]})")

    # 4) 更新分支 ref（force 覆盖旧历史）
    latest = created[head]
    result = api("PATCH", f"/refs/{BRANCH}", {"sha": latest, "force": True})
    print(f"分支 {BRANCH} 已更新 -> {latest}")
    print(f"完成！仓库: https://github.com/{OWNER}/{REPO}")


if __name__ == "__main__":
    TOKEN = sys.argv[1] if len(sys.argv) > 1 else os.environ.get("GITHUB_TOKEN", "")
    main()
