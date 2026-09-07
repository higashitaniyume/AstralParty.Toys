// home.js - Welcome Page, Hero Portrait Interaction, Announcements & Navigation Cards
(function () {
  const HomeModule = {
    init(data) {
      if (!data) return;
      this.renderAnnouncements(data.announcements || []);
      this.pickRandomPortrait();
      this.setupEventListeners();
    },

    onShowHome() {
      // If no portrait loaded yet, pick one
      if (!AppState.currentPortrait && AppState.homeData?.portraits?.length) {
        this.pickRandomPortrait();
      }
    },

    setupEventListeners() {
      // 3 Bottom Action Cards
      $('cardWiki')?.addEventListener('click', () => showPage('wiki'));
      $('cardTools')?.addEventListener('click', () => showPage('tools'));
      $('cardSettings')?.addEventListener('click', () => showPage('settings'));

      // Switch Hero Button
      $('switchHeroBtn')?.addEventListener('click', e => {
        e.stopPropagation();
        this.pickRandomPortrait();
      });

      // Portrait Click Interaction
      $('heroPortraitWrapper')?.addEventListener('click', () => {
        this.triggerInteraction(true);
      });

      // Speech Bubble Click
      $('heroDialogBubble')?.addEventListener('click', () => {
        this.triggerInteraction(false);
      });
    },

    pickRandomPortrait() {
      const portraits = AppState.homeData?.portraits;
      if (!portraits || !portraits.length) {
        // Fallback default
        this.applyPortrait({
          id: 'default',
          heroId: 101,
          heroName: '帕露南',
          title: '商业之主',
          variant: '默认立绘',
          url: 'https://assets.astral.local/Characters/101.webp'
        });
        return;
      }

      // Avoid picking the exact same portrait consecutively if multiple are available
      let candidate = portraits[Math.floor(Math.random() * portraits.length)];
      if (portraits.length > 1 && AppState.currentPortrait && candidate.id === AppState.currentPortrait.id) {
        candidate = portraits[Math.floor(Math.random() * portraits.length)];
      }

      AppState.currentPortrait = candidate;
      this.applyPortrait(candidate);
      this.triggerInteraction(false);
    },

    applyPortrait(item) {
      const img = $('heroPortraitImg');
      const nameEl = $('heroNameTag');
      const titleEl = $('heroTitleTag');
      const variantEl = $('heroVariantTag');

      if (img) {
        img.style.opacity = '0';
        img.style.transform = 'scale(0.95)';
        setTimeout(() => {
          img.src = item.url;
          img.alt = item.heroName;
          img.onload = () => {
            img.style.opacity = '1';
            img.style.transform = 'scale(1)';
          };
          img.onerror = () => {
            // Fallback to square avatar if full webp fails
            img.src = `https://assets.astral.local/Characters/${item.heroId}.webp`;
            img.style.opacity = '1';
            img.style.transform = 'scale(1)';
          };
        }, 150);
      }

      if (nameEl) nameEl.textContent = item.heroName;
      if (titleEl) titleEl.textContent = item.title;
      if (variantEl) variantEl.textContent = item.variant || '默认';
      if ($('bubbleSpeakerName')) $('bubbleSpeakerName').textContent = item.heroName;
    },

    triggerInteraction(isClick) {
      const p = AppState.currentPortrait;
      const data = AppState.homeData;
      if (!data) return;

      let line = '';
      if (isClick && data.clickReactions?.length) {
        line = data.clickReactions[Math.floor(Math.random() * data.clickReactions.length)];
      } else {
        const heroLines = p && data.heroGreetings?.[p.heroId];
        if (heroLines && heroLines.length && Math.random() > 0.35) {
          line = heroLines[Math.floor(Math.random() * heroLines.length)];
        } else if (data.greetings?.length) {
          line = data.greetings[Math.floor(Math.random() * data.greetings.length)];
        }
      }

      const bubbleText = $('bubbleSpeechText');
      if (bubbleText) {
        bubbleText.style.opacity = '0.3';
        bubbleText.style.transform = 'translateY(2px)';
        setTimeout(() => {
          bubbleText.textContent = line;
          bubbleText.style.opacity = '1';
          bubbleText.style.transform = 'translateY(0)';
        }, 120);
      }
    },

    renderAnnouncements(list) {
      const container = $('announcementsList');
      if (!container) return;

      if (!list.length) {
        container.innerHTML = '<p class="field-desc">暂无公告信息</p>';
        return;
      }

      if ($('announcementsCount')) {
        $('announcementsCount').textContent = `${list.length} 条`;
      }

      container.innerHTML = list.map(item => `
        <article class="announcement-card" data-notice-id="${esc(item.id)}">
          <div class="announcement-meta">
            <span class="badge ${esc(item.tagClass)}">${esc(item.category)}</span>
            <span class="announcement-date">${esc(item.date)}</span>
          </div>
          <h4>${esc(item.title)}</h4>
          <p class="announcement-snippet">${esc(item.summary)}</p>
        </article>
      `).join('');

      container.querySelectorAll('.announcement-card').forEach(card => {
        card.addEventListener('click', () => {
          const id = card.dataset.noticeId;
          const target = list.find(x => x.id === id);
          if (target) {
            openModal(target.title, `
              <div style="margin-bottom:14px;display:flex;align-items:center;gap:10px;">
                <span class="badge ${esc(target.tagClass)}">${esc(target.category)}</span>
                <span style="font-size:12px;color:var(--text-muted)">发布日期：${esc(target.date)}</span>
              </div>
              <div style="font-size:14px;line-height:1.8;color:var(--text-main);white-space:pre-line;">
                ${esc(target.content)}
              </div>
            `);
          }
        });
      });
    }
  };

  window.HomeModule = HomeModule;
})();
