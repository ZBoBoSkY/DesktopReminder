# -*- coding: utf-8 -*-
"""任务模型与计时逻辑。

任务三态：running(计时中) -> done(预计已完成，等待确认) -> ack(已确认，归档)
"""
import uuid
from datetime import datetime, timedelta

STATUS_RUNNING = "running"
STATUS_DONE = "done"
STATUS_ACK = "ack"


def _now():
    return datetime.now()


class Task:
    def __init__(self, title, duration_seconds, created_at=None, tid=None,
                 status=STATUS_RUNNING, completion_at=None, last_notified=None):
        self.id = tid or uuid.uuid4().hex[:10]
        self.title = (title or "").strip()
        self.duration_seconds = max(1, int(duration_seconds))
        self.created_at = created_at or _now()
        self.status = status
        self.completion_at = completion_at
        self.last_notified = last_notified

    @property
    def due_at(self):
        return self.created_at + timedelta(seconds=self.duration_seconds)

    def remaining_seconds(self, now=None):
        now = now or _now()
        return max(0, (self.due_at - now).total_seconds())

    def done_minutes(self, now=None):
        if self.completion_at is None:
            return 0
        now = now or _now()
        return int((now - self.completion_at).total_seconds() // 60)

    def to_dict(self):
        return {
            "id": self.id,
            "title": self.title,
            "duration_seconds": self.duration_seconds,
            "created_at": self.created_at.isoformat(),
            "status": self.status,
            "completion_at": self.completion_at.isoformat() if self.completion_at else None,
            "last_notified": self.last_notified.isoformat() if self.last_notified else None,
        }

    @classmethod
    def from_dict(cls, d):
        def _dt(v):
            try:
                return datetime.fromisoformat(v) if v else None
            except Exception:
                return None
        return cls(
            title=d.get("title", ""),
            duration_seconds=d.get("duration_seconds", 60),
            created_at=_dt(d.get("created_at")) or _now(),
            tid=d.get("id"),
            status=d.get("status", STATUS_RUNNING),
            completion_at=_dt(d.get("completion_at")),
            last_notified=_dt(d.get("last_notified")),
        )


class TaskManager:
    def __init__(self, save_cb=None):
        self.tasks = []
        self.save_cb = save_cb

    def load(self, raw_list):
        self.tasks = []
        for d in (raw_list or []):
            try:
                t = Task.from_dict(d)
            except Exception:
                continue
            if t.status == STATUS_ACK:
                continue                      # 已确认的不再载入
            if (t.status == STATUS_DONE and t.completion_at
                    and t.completion_at < _now() - timedelta(hours=24)):
                continue                      # 超过一天的旧完成记录丢弃
            self.tasks.append(t)
        for t in self.tasks:
            if t.status == STATUS_DONE and t.completion_at is None:
                t.completion_at = t.due_at
        self._save()

    def add(self, title, duration_seconds):
        t = Task(title, duration_seconds)
        self.tasks.append(t)
        self._save()
        return t

    def ack(self, tid):
        for t in self.tasks:
            if t.id == tid:
                t.status = STATUS_ACK
                self._save()
                return True
        return False

    def snooze(self, tid, minutes):
        """推迟提醒：将任务重新计时 N 分钟。"""
        for t in self.tasks:
            if t.id == tid:
                now = _now()
                t.created_at = now
                t.duration_seconds = max(1, int(minutes) * 60)
                t.status = STATUS_RUNNING
                t.completion_at = None
                t.last_notified = None
                self._save()
                return True
        return False

    def remove(self, tid):
        self.tasks = [t for t in self.tasks if t.id != tid]
        self._save()

    def unacked_done(self):
        return [t for t in self.tasks if t.status == STATUS_DONE]

    def running_count(self):
        return sum(1 for t in self.tasks if t.status == STATUS_RUNNING)

    def has_active_tasks(self):
        """是否有 running 或 done 状态的任务。"""
        return any(t.status in (STATUS_RUNNING, STATUS_DONE) for t in self.tasks)

    def _save(self):
        if self.save_cb:
            self.save_cb([t.to_dict() for t in self.tasks
                          if t.status != STATUS_ACK])

    def tick(self, now=None, escalation_minutes=10):
        """推进任务状态，返回需要提醒的事件列表。

        事件：{"kind": "done", "task": t}
              {"kind": "escalate", "task": t, "minutes": n}
        """
        now = now or _now()
        events = []
        for t in self.tasks:
            if t.status == STATUS_RUNNING and now >= t.due_at:
                t.status = STATUS_DONE
                t.completion_at = t.due_at
                t.last_notified = now
                events.append({"kind": "done", "task": t})
            elif t.status == STATUS_DONE:
                mins = t.done_minutes(now)
                if mins >= escalation_minutes:
                    since_last = (now - t.last_notified).total_seconds() \
                        if t.last_notified else float("inf")
                    if since_last >= (escalation_minutes * 60 - 5):
                        t.last_notified = now
                        events.append({"kind": "escalate", "task": t,
                                       "minutes": mins})
        if events:
            self._save()
        return events
