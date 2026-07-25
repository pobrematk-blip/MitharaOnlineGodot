// Mithara Online - Site JS

document.addEventListener('DOMContentLoaded', function() {
    // Mobile nav toggle
    var navToggle = document.querySelector('.nav-toggle');
    var navLinks = document.querySelector('.nav-links');

    if (navToggle) {
        navToggle.addEventListener('click', function() {
            navLinks.classList.toggle('open');
        });
    }

    // Close nav on link click (mobile)
    document.querySelectorAll('.nav-links a').forEach(function(link) {
        link.addEventListener('click', function() {
            navLinks.classList.remove('open');
        });
    });

    // Ranking tabs
    var tabBtns = document.querySelectorAll('.tab-btn');
    tabBtns.forEach(function(btn) {
        btn.addEventListener('click', function() {
            var tab = this.dataset.tab;
            if (!tab) return;

            tabBtns.forEach(function(b) { b.classList.remove('active'); });
            this.classList.add('active');

            document.querySelectorAll('.tab-content').forEach(function(c) {
                c.classList.remove('active');
            });

            var target = document.getElementById('tab-' + tab);
            if (target) target.classList.add('active');
        });
    });

    // Auto-hide alerts after 5 seconds
    var alerts = document.querySelectorAll('.alert');
    alerts.forEach(function(alert) {
        setTimeout(function() {
            alert.style.transition = 'opacity 0.5s';
            alert.style.opacity = '0';
            setTimeout(function() {
                alert.style.display = 'none';
            }, 500);
        }, 5000);
    });

    var statusBox = document.querySelector('[data-site-status]');
    var statusText = document.querySelector('[data-site-status-text]');

    function updateStatus(snapshot) {
        if (!statusBox || !statusText || !snapshot) return;
        statusBox.classList.remove('offline');
        statusBox.classList.add('online');
        var names = Array.isArray(snapshot.onlinePlayerNames) ? snapshot.onlinePlayerNames : [];
        var visibleNames = names.slice(0, 3).join(', ');
        var extraNames = names.length > 3 ? ' +' + (names.length - 3) : '';
        var onlineLabel = visibleNames
            ? snapshot.onlinePlayers + ' online: ' + visibleNames + extraNames
            : snapshot.onlinePlayers + ' online';
        statusText.textContent = onlineLabel + ' | ' +
            snapshot.totalCharacters + ' personagens';
        statusBox.title = names.length ? 'Online: ' + names.join(', ') : '';
    }

    function setStatusOffline() {
        if (!statusBox || !statusText) return;
        statusBox.classList.remove('online');
        statusBox.classList.add('offline');
        statusText.textContent = 'Status indisponivel';
    }

    if (statusBox && window.EventSource) {
        var source = new EventSource('/api/site-status/stream');
        source.addEventListener('status', function(event) {
            try {
                updateStatus(JSON.parse(event.data));
            } catch {
                setStatusOffline();
            }
        });
        source.onerror = function() {
            setStatusOffline();
        };
    } else if (statusBox) {
        fetch('/api/site-status')
            .then(function(response) { return response.json(); })
            .then(updateStatus)
            .catch(setStatusOffline);
    }
});
