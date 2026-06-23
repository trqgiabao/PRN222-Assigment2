(function () {
    'use strict';

    function escapeHtml(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function shouldShowSubject(subject, role, userId) {
        if (!subject) return false;
        if (subject.isActive === false) return false;
        if (role === 'Admin') return true;
        if (role === 'Teacher') return subject.teacherId === userId;
        if (role === 'Student') return !!subject.hasMaterials;
        return false;
    }

    function buildTeacherCell(subject, role) {
        if (role !== 'Admin') {
            return escapeHtml(subject.teacherName || '—');
        }
        if (subject.teacherId && subject.teacherName) {
            return '<span class="badge badge-teacher-assigned">' + escapeHtml(subject.teacherName) + '</span>';
        }
        if (subject.isActive === false) {
            return '<span class="text-muted">—</span>';
        }
        return '<button type="button" class="badge badge-assign-teacher border-0 js-assign-teacher" ' +
            'data-subject-id="' + subject.id + '" data-subject-name="' + escapeHtml(subject.name) + '">' +
            'Gán giáo viên</button>';
    }

    function buildActionButtons(subject, role, urls) {
        var id = subject.id;
        var html = '<a href="' + urls.details + '?id=' + id + '" class="btn btn-sm btn-outline-primary">Chi tiết</a> ';

        if (role === 'Admin') {
            html += '<a href="' + urls.edit + '?id=' + id + '" class="btn btn-sm btn-outline-secondary">Sửa</a> ';
        }

        if (role === 'Admin' || role === 'Teacher') {
            html += '<a href="' + urls.upload + '?subjectId=' + id + '" class="btn btn-sm btn-primary">Tải lên</a>';
        }

        if (role === 'Student' && subject.hasMaterials) {
            html += '<a href="' + urls.materials + '?subjectId=' + id + '" class="btn btn-sm btn-outline-success">Tải về</a> ';
            html += '<a href="' + urls.chatCreate + '?subjectId=' + id + '" class="btn btn-sm btn-primary">Chat</a>';
        }

        return html;
    }

    function buildMaterialsCell(subject, role, urls) {
        var count = subject.documentCount || 0;
        if (!subject.hasMaterials || count <= 0) return '<span class="text-muted">Chưa có tài liệu</span>';
        return '<a href="' + urls.details + '?id=' + subject.id + '" class="badge badge-doc badge-doc-link text-decoration-none">' + count + ' tài liệu</a>';
    }

    function buildSubjectRow(subject, role, urls) {
        var teacherId = subject.teacherId || '';
        var tr = document.createElement('tr');
        tr.setAttribute('data-subject-id', subject.id);
        tr.setAttribute('data-teacher-id', teacherId);
        tr.setAttribute('data-has-materials', subject.hasMaterials ? 'true' : 'false');
        tr.innerHTML =
            '<td><strong>' + escapeHtml(subject.name) + '</strong></td>' +
            '<td>' + buildTeacherCell(subject, role) + '</td>' +
            '<td class="text-muted">' + escapeHtml(subject.description || '—') + '</td>' +
            '<td>' + buildMaterialsCell(subject, role, urls) + '</td>' +
            '<td class="text-nowrap">' + buildActionButtons(subject, role, urls) + '</td>';
        return tr;
    }

    function updateSubjectRow(row, subject, role, urls) {
        row.setAttribute('data-teacher-id', subject.teacherId || '');
        row.setAttribute('data-has-materials', subject.hasMaterials ? 'true' : 'false');
        row.children[0].innerHTML = '<strong>' + escapeHtml(subject.name) + '</strong>';
        row.children[1].innerHTML = buildTeacherCell(subject, role);
        row.children[2].textContent = subject.description || '—';
        row.children[3].innerHTML = buildMaterialsCell(subject, role, urls);
        row.children[4].innerHTML = buildActionButtons(subject, role, urls);
    }

    function removeSubjectRows(tbody, subjectId) {
        tbody.querySelectorAll('tr[data-subject-id="' + subjectId + '"]').forEach(function (row) {
            row.remove();
        });
    }

    function toggleEmptyState(panel, emptyAlert, tbody) {
        var hasRows = tbody.querySelectorAll('tr[data-subject-id]').length > 0;
        if (emptyAlert) {
            emptyAlert.classList.toggle('d-none', hasRows);
        }
        if (panel) {
            panel.classList.toggle('d-none', !hasRows);
        }
    }

    function showToast(message) {
        var toast = document.getElementById('subject-realtime-toast');
        if (!toast) return;
        toast.textContent = message;
        toast.classList.remove('d-none');
        clearTimeout(toast._hideTimer);
        toast._hideTimer = setTimeout(function () {
            toast.classList.add('d-none');
        }, 4000);
    }

    window.initSubjectRealtime = function (options) {
        if (!options.hubUrl || !window.signalR) return;

        var role = options.role;
        var userId = options.userId || '';
        var urls = options.urls || {};
        var tbody = document.getElementById('subjects-tbody');
        var panel = document.getElementById('subjects-panel');
        var emptyAlert = document.getElementById('subjects-empty');
        var currentSubjectId = options.currentSubjectId || null;

        if (!tbody && !currentSubjectId) return;

        var connection = new signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl)
            .withAutomaticReconnect()
            .build();

        connection.on('SubjectChanged', function (evt) {
            var action = evt.action;
            var subjectId = evt.subjectId;
            var subject = evt.subject;
            var previousTeacherId = evt.previousTeacherId;

            if (currentSubjectId && subjectId === currentSubjectId) {
                if (action === 'Deleted') {
                    showToast('Môn học này đã bị xóa.');
                    setTimeout(function () {
                        window.location.href = urls.index || '/Subjects';
                    }, 1200);
                    return;
                }
                if (action === 'Updated' || action === 'TeacherAssigned' || action === 'TeacherUnassigned') {
                    if (!shouldShowSubject(subject, role, userId)) {
                        showToast('Môn học này không còn khả dụng.');
                        setTimeout(function () {
                            window.location.href = urls.index || '/Subjects';
                        }, 1200);
                    }
                }
            }

            if (!tbody) return;

            if (action === 'Deleted') {
                removeSubjectRows(tbody, subjectId);
                toggleEmptyState(panel, emptyAlert, tbody);
                showToast('Môn học đã bị xóa khỏi danh sách.');
                return;
            }

            if (action === 'MaterialsRemoved' && role === 'Student') {
                removeSubjectRows(tbody, subjectId);
                toggleEmptyState(panel, emptyAlert, tbody);
                showToast('Môn học không còn tài liệu.');
                return;
            }

            if (action === 'TeacherUnassigned' && previousTeacherId === userId && role === 'Teacher') {
                removeSubjectRows(tbody, subjectId);
                toggleEmptyState(panel, emptyAlert, tbody);
                showToast('Bạn đã được gỡ khỏi môn học.');
                return;
            }

            if (!subject || !shouldShowSubject(subject, role, userId)) {
                if (action === 'TeacherAssigned' && previousTeacherId === userId && role === 'Teacher') {
                    removeSubjectRows(tbody, subjectId);
                    toggleEmptyState(panel, emptyAlert, tbody);
                }
                return;
            }

            var existing = tbody.querySelector('tr[data-subject-id="' + subject.id + '"]:not(.chunk-expand-row)');
            if (existing) {
                updateSubjectRow(existing, subject, role, urls);
                showToast('Môn học "' + subject.name + '" đã được cập nhật.');
            } else {
                tbody.appendChild(buildSubjectRow(subject, role, urls));
                toggleEmptyState(panel, emptyAlert, tbody);
                showToast('Môn học mới: "' + subject.name + '".');
            }
        });

        connection.onreconnected(function () {
            return connection.invoke('JoinSubjectsFeed');
        });

        connection.start()
            .then(function () { return connection.invoke('JoinSubjectsFeed'); })
            .catch(function (err) {
                console.warn('Subject realtime connection failed:', err);
            });
    };
})();
