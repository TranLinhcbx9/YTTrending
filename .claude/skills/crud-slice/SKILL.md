---
name: crud-slice
description: Scaffold một CRUD feature slice đầy đủ (Add/Update/Delete/GetById/GetPaged) cho một Domain entity trong codebase YTTrending (.NET 8), dựa trên feature Channel làm mẫu chuẩn (CQRS + MediatR, Repository/UnitOfWork, Result pattern, EF Core 8 + Postgres). Dùng skill này khi user yêu cầu scaffold/thêm CRUD cho một entity, hoặc chỉ cần một phần trong Create/Add, Update/Edit, Delete, GetById, GetPaged/List — ví dụ "scaffold CRUD cho Tag", "thêm CRUD cho SavedIdea", "tạo command Add/Update cho X", "cần list + get by id cho Playlist", hoặc "thêm entity mới end-to-end". Cũng trigger khi user muốn thêm một entity hoàn toàn mới xuyên suốt Domain → Application → Infrastructure → API.
---

Đọc đầy đủ và thực hiện workflow tại
[`../../../.agents/skills/crud-slice/SKILL.md`](../../../.agents/skills/crud-slice/SKILL.md).

Mọi đường dẫn tương đối trong workflow được tính từ thư mục skill chính
`.agents/skills/crud-slice/`.
