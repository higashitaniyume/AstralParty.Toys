// workshop/resources.js - Resource market rendering, speed/resume display & preview gallery
(function () {
  'use strict';

  function formatSpeed(bytesPerSecond) {
    const val = Number(bytesPerSecond || 0);
    if (!Number.isFinite(val) || val <= 0) return '';
    if (val >= 1024 * 1024) return `${(val / 1024 / 1024).toFixed(1)} MB/s`;
    if (val >= 1024) return `${(val / 1024).toFixed(0)} KB/s`;
    return `${Math.round(val)} B/s`;
  }

  function formatProgressDetail(progress, t, formatBytes) {
    if (!progress) return t('workshop.preparing');
    if (progress.phase === 'prepare') return t('workshop.preparing');
    if (progress.phase === 'verify') return t('workshop.verifying');
    if (progress.phase === 'install' || progress.phase === 'device') {
      return progress.message || t('workshop.installing');
    }

    const parts = [];
    if (typeof progress.percent === 'number') {
      parts.push(t('workshop.downloading_percent', { percent: progress.percent }));
    } else {
      parts.push(t('workshop.downloading'));
    }

    const received = Number(progress.receivedBytes || 0);
    const total = Number(progress.totalBytes || 0);
    if (received > 0 && total > 0) {
      parts.push(`${formatBytes(received)} / ${formatBytes(total)}`);
    } else if (received > 0) {
      parts.push(formatBytes(received));
    }

    const speed = formatSpeed(progress.bytesPerSecond);
    if (speed) parts.push(speed);

    if (progress.activeThreads > 1) {
      parts.push(t('workshop.threads_badge', { count: progress.activeThreads }));
    }
    if (progress.resumed) {
      parts.push(t('workshop.resumed_badge'));
    }

    return parts.join(' · ');
  }

  function ratingLabel(rating, t) {
    const key = String(rating || 'general').toLowerCase();
    if (key === 'teen') return t('workshop.rating_teen');
    if (key === 'mature') return t('workshop.rating_mature');
    return '';
  }

  function typeLabel(item) {
    return item.typeName || item.type || '';
  }

  function formatAttributes(attrs) {
    if (!attrs || typeof attrs !== 'object') return [];
    const pills = [];
    if (attrs.cardCount) pills.push(`${attrs.cardCount} 张卡面`);
    if (attrs.hasAnimation) pills.push('含动态卡面');
    if (attrs.heroId) pills.push(`角色 #${attrs.heroId}`);
    if (attrs.isScheme) pills.push('完整方案包');
    if (attrs.audioCount) pills.push(`${attrs.audioCount} 条音效`);
    if (attrs.hasVideo) pills.push('动态视频');
    if (attrs.hasImage) pills.push('静态图片');
    if (attrs.width && attrs.height) pills.push(`${attrs.width}×${attrs.height}`);
    if (attrs.bundleCount) pills.push(`${attrs.bundleCount} 个 Bundle`);
    return pills;
  }

  function actionForResource(item, ctx) {
    const installed = item.installedVersion || '';
    const latest = item.latestVersion || '';
    if (!installed) {
      return {
        label: ctx.t('workshop.install_version', { version: latest }),
        install: true,
        version: latest
      };
    }
    if (ctx.compareVersions(installed, latest) < 0) {
      return {
        label: ctx.t('workshop.update_version', { version: latest }),
        install: true,
        version: latest,
        upgrade: true
      };
    }
    return {
      label: ctx.t('workshop.reinstall_version', { version: installed }),
      install: true,
      version: latest
    };
  }

  function renderCard(item, ctx) {
    const ratingText = ratingLabel(item.contentRating, ctx.t);
    const typeName = typeLabel(item);
    return `
      <article class="workshop-card workshop-resource-card" data-mod-id="${esc(item.id)}">
        <button class="workshop-cover" type="button" data-open="${esc(item.id)}" aria-label="${esc(item.name)}">
          ${ctx.coverMarkup(item)}
          ${item.featured ? `<span class="workshop-featured">${esc(ctx.t('workshop.featured'))}</span>` : ''}
          ${typeName ? `<span class="workshop-type-badge">${esc(typeName)}</span>` : ''}
          ${ratingText ? `<span class="workshop-rating-badge">${esc(ratingText)}</span>` : ''}
        </button>
        <div class="workshop-card-body">
          <div class="workshop-card-head">
            <h3 title="${esc(item.name)}">${esc(item.name)}</h3>
            <span class="workshop-version">v${esc(item.latestVersion)}</span>
          </div>
          <p class="workshop-card-summary">${esc(item.summary || ctx.t('workshop.no_summary'))}</p>
          ${
            item.tags && item.tags.length
              ? `<div class="workshop-tags">${item.tags.slice(0, 3).map(tag => `<span>${esc(tag)}</span>`).join('')}</div>`
              : ''
          }
          <div class="workshop-card-meta">
            <span>${esc(item.author || ctx.t('workshop.anonymous'))}</span>
            <span aria-hidden="true">·</span>
            <span>${ctx.formatBytes(item.sizeBytes)}</span>
            <span aria-hidden="true">·</span>
            <span>${ctx.t('workshop.downloads_short', { count: fmt(item.downloads) })}</span>
          </div>
          <div class="workshop-card-foot">
            <span class="workshop-chip-row">${ctx.chipFor(item)}</span>
            <button class="secondary-btn workshop-install-btn" data-role="action" type="button"></button>
          </div>
          <div class="workshop-card-progress" hidden><span></span></div>
        </div>
      </article>
    `;
  }

  function renderRow(item, ctx) {
    const ratingText = ratingLabel(item.contentRating, ctx.t);
    const typeName = typeLabel(item);
    return `
      <article class="workshop-row workshop-resource-row" data-mod-id="${esc(item.id)}">
        <div class="workshop-row-thumb">
          ${ctx.coverMarkup(item)}
        </div>
        <div class="workshop-row-main">
          <div class="workshop-row-header">
            <h3 class="workshop-row-title" title="${esc(item.name)}">${esc(item.name)}</h3>
            ${typeName ? `<span class="workshop-type-pill-inline">${esc(typeName)}</span>` : ''}
            ${ratingText ? `<span class="workshop-rating-pill-inline">${esc(ratingText)}</span>` : ''}
            <span class="workshop-version">v${esc(item.latestVersion)}</span>
            ${ctx.chipFor(item)}
            ${item.featured ? `<span class="workshop-featured" style="position:static;padding:2px 8px;font-size:10.5px;">${esc(ctx.t('workshop.featured'))}</span>` : ''}
          </div>
          <p class="workshop-row-desc">${esc(item.summary || ctx.t('workshop.no_summary'))}</p>
          <div class="workshop-row-meta">
            <span>${esc(item.author || ctx.t('workshop.anonymous'))}</span>
            <span aria-hidden="true">·</span>
            <span>${ctx.formatBytes(item.sizeBytes)}</span>
            ${item.fileCount > 0 ? `<span aria-hidden="true">·</span><span>${ctx.t('workshop.file_count', { count: fmt(item.fileCount) })}</span>` : ''}
            <span aria-hidden="true">·</span>
            <span>${ctx.t('workshop.downloads_short', { count: fmt(item.downloads) })}</span>
            ${
              item.tags && item.tags.length
                ? `<div class="workshop-row-tags">${item.tags.slice(0, 3).map(tag => `<span>${esc(tag)}</span>`).join('')}</div>`
                : ''
            }
          </div>
        </div>
        <div class="workshop-row-actions">
          <div class="workshop-row-progress" hidden><span></span></div>
          <button class="secondary-btn workshop-install-btn" data-role="action" type="button"></button>
        </div>
      </article>
    `;
  }

  function commentUrl(baseUrl, id) {
    const base = String(baseUrl || AppState.librarySettings?.workshopUrl || 'https://forge.hiynet.com').replace(/\/+$/, '');
    if (!/^https?:\/\//i.test(base) || !id) return '';
    return `${base}/resources/${encodeURIComponent(id)}#comments`;
  }

  function renderDetailAction(res, ctx) {
    if (ctx.state.installingId === res.id) {
      return `<button class="secondary-btn is-busy" id="workshopDetailCancelBtn" type="button">${esc(
        ctx.t('workshop.cancel')
      )}</button>`;
    }
    const action = actionForResource(res, ctx);
    return `<button class="primary-btn" type="button" data-install="${esc(res.id)}" data-version="${esc(
      action.version
    )}">${esc(action.label)}</button>`;
  }

  function renderDetailHero(res, ctx) {
    const latest = (res.versions || []).find(v => v.version === res.latestVersion) || (res.versions || [])[0];
    const comments = commentUrl(ctx.state.baseUrl, res.id);
    const ratingText = ratingLabel(res.contentRating, ctx.t);
    const typeName = typeLabel(res);
    const jump = ctx.state.lastInstalledJump;
    const jumpBtn = jump && jump.id === res.id && jump.jumpPage
      ? `<button class="secondary-btn" type="button" data-jump-page="${esc(jump.jumpPage)}">${esc(
          ctx.t('workshop.open_manage')
        )}</button>`
      : '';

    return `
      <div class="workshop-detail-hero">
        <div class="workshop-detail-cover">${ctx.coverMarkup(res)}</div>
        <div class="workshop-detail-main">
          <div class="workshop-detail-title">
            <h3>${esc(res.name || res.id)}</h3>
            ${typeName ? `<span class="workshop-type-pill-inline">${esc(typeName)}</span>` : ''}
            ${ratingText ? `<span class="workshop-rating-pill-inline">${esc(ratingText)}</span>` : ''}
            ${res.featured ? `<span class="workshop-mini-badge">${esc(ctx.t('workshop.featured'))}</span>` : ''}
          </div>
          <p class="workshop-detail-summary">${esc(res.summary || ctx.t('workshop.no_summary'))}</p>
          ${
            res.tags && res.tags.length
              ? `<div class="workshop-tags">${res.tags.map(tag => `<span>${esc(tag)}</span>`).join('')}</div>`
              : ''
          }
          <div class="workshop-detail-meta">
            <span>${esc(ctx.t('workshop.author'))} ${esc(res.author || ctx.t('workshop.anonymous'))}</span>
            <span>${esc(ctx.t('workshop.latest'))} v${esc(res.latestVersion || '—')}</span>
            ${latest ? `<span>${ctx.formatBytes(latest.sizeBytes)}</span>` : ''}
            <span>${ctx.t('workshop.downloads_short', { count: fmt(res.downloads) })}</span>
            <span>${esc(ctx.t('workshop.updated'))} ${ctx.formatDate(res.updatedAt)}</span>
          </div>
          <div class="workshop-detail-actions">
            ${ctx.chipFor(res)}
            <span id="workshopDetailAction">${renderDetailAction(res, ctx)}</span>
            ${jumpBtn}
            ${
              comments
                ? `<a class="secondary-btn" style="text-decoration:none" href="${esc(
                    comments
                  )}" target="_blank" rel="noopener noreferrer">${esc(ctx.t('workshop.comments'))}</a>`
                : ''
            }
          </div>
          <div class="workshop-progress hidden" id="workshopDetailProgress">
            <div class="workshop-progress-bar"><span id="workshopDetailProgressBar"></span></div>
            <span class="workshop-progress-text" id="workshopDetailProgressText"></span>
          </div>
        </div>
      </div>
      ${renderPreviewGallery(res, ctx)}
    `;
  }

  function renderPreviewGallery(res, ctx) {
    const previews = Array.isArray(res.previews) ? res.previews.filter(p => p && p.url) : [];
    if (!previews.length) return '';

    return `
      <section class="workshop-preview-gallery" aria-label="${esc(ctx.t('workshop.preview_gallery'))}">
        <div class="workshop-preview-head">
          <h4>${esc(ctx.t('workshop.preview_gallery'))}</h4>
          <span>${esc(ctx.t('workshop.preview_count', { count: previews.length }))}</span>
        </div>
        <div class="workshop-preview-strip">
          ${previews
            .map(
              (p, idx) => `
              <button type="button" class="workshop-preview-thumb" data-preview-index="${idx}" title="${esc(p.caption || p.sourcePath || '')}">
                <img src="${esc(p.url)}" alt="${esc(p.caption || res.name || '')}" loading="lazy" />
              </button>`
            )
            .join('')}
        </div>
      </section>
    `;
  }

  function openPreviewLightbox(previews, startIndex, resourceTitle) {
    const list = Array.isArray(previews) ? previews.filter(p => p && p.url) : [];
    if (!list.length) return;

    let current = Math.max(0, Math.min(Number(startIndex) || 0, list.length - 1));

    function renderBox() {
      const item = list[current];
      const caption = item.caption || item.sourcePath || '';
      const html = `
        <div class="workshop-lightbox">
          <div class="workshop-lightbox-stage">
            <img src="${esc(item.url)}" alt="${esc(caption || resourceTitle || '')}" />
          </div>
          <div class="workshop-lightbox-bar">
            <span class="workshop-lightbox-caption">${esc(caption)}</span>
            <div class="workshop-lightbox-nav">
              <button type="button" class="secondary-btn" id="workshopLbPrev" ${list.length <= 1 ? 'disabled' : ''}>‹</button>
              <span>${current + 1} / ${list.length}</span>
              <button type="button" class="secondary-btn" id="workshopLbNext" ${list.length <= 1 ? 'disabled' : ''}>›</button>
            </div>
          </div>
        </div>
      `;
      openModal(resourceTitle || '', html, '', 'modal-wide');
      $('workshopLbPrev')?.addEventListener('click', () => {
        current = (current - 1 + list.length) % list.length;
        renderBox();
      });
      $('workshopLbNext')?.addEventListener('click', () => {
        current = (current + 1) % list.length;
        renderBox();
      });
    }

    renderBox();
  }

  function renderDetailFacts(res, ctx) {
    const latest = (res.versions || []).find(v => v.version === res.latestVersion) || (res.versions || [])[0];
    const modDeps = Array.isArray(latest?.modDependencies) ? latest.modDependencies : [];
    const resDeps = Array.isArray(latest?.resourceDependencies) ? latest.resourceDependencies : [];
    const attrs = formatAttributes(res.attributes);
    const credits = Array.isArray(res.credits) ? res.credits : [];

    const fact = (label, value, extra) =>
      `<div class="workshop-fact"><dt>${esc(label)}</dt><dd${extra ? ` class="${extra}"` : ''}>${value}</dd></div>`;

    const repositoryUrl = ctx.externalUrl(res.repositoryUrl);
    const repository = repositoryUrl
      ? `<a href="${esc(repositoryUrl)}" target="_blank" rel="noopener noreferrer">${esc(repositoryUrl)}</a>`
      : esc(ctx.t('workshop.not_provided'));

    const depHtml = (dep, isMod) => {
      const ver = dep.minVersion ? ` ≥ ${dep.minVersion}` : '';
      if (!isMod) {
        return `<span class="workshop-dep-pill">${esc(dep.id + ver)}</span>`;
      }
      const cls = dep.installed ? 'is-ok' : 'is-missing';
      const badge = dep.installed
        ? `${esc(dep.id + ver)} (${esc(ctx.t('workshop.dep_installed'))})`
        : `${esc(dep.id + ver)} (${esc(ctx.t('workshop.dep_missing'))})`;
      return `<span class="workshop-dep-pill ${cls}">${badge}</span>`;
    };

    const facts = `
      <dl class="workshop-facts">
        ${fact(ctx.t('workshop.resource_id'), esc(res.id), 'is-mono')}
        ${fact(ctx.t('workshop.resource_type'), esc(typeLabel(res) || '—'))}
        ${fact(ctx.t('workshop.author'), esc(res.author || ctx.t('workshop.anonymous')))}
        ${fact(ctx.t('workshop.latest'), latest ? `v${esc(latest.version)}` : '—', 'is-mono')}
        ${fact(ctx.t('workshop.package_size'), latest ? ctx.formatBytes(latest.sizeBytes) : '—')}
        ${latest?.fileCount ? fact(ctx.t('workshop.files'), fmt(latest.fileCount)) : ''}
        ${latest?.gameVersion ? fact(ctx.t('workshop.game_version'), esc(latest.gameVersion)) : ''}
        ${latest && ctx.describePlatforms(latest.platforms) ? fact(ctx.t('workshop.platforms'), esc(ctx.describePlatforms(latest.platforms))) : ''}
        ${fact(ctx.t('workshop.downloads'), fmt(res.downloads))}
        ${fact(ctx.t('workshop.updated'), ctx.formatDate(res.updatedAt))}
        ${fact(ctx.t('workshop.repository'), repository)}
        ${fact(ctx.t('workshop.license'), esc(res.license || ctx.t('workshop.license_none')), res.license ? 'is-mono' : '')}
        ${fact(ctx.t('workshop.hosting'), esc(latest?.hosted ? ctx.t('workshop.hosted_by_market') : ctx.t('workshop.hosted_by_author')))}
      </dl>
    `;

    const extraBlocks = [];
    if (attrs.length) {
      extraBlocks.push(`<div class="workshop-detail-section"><h4>${esc(ctx.t('workshop.attributes'))}</h4><div class="workshop-tags">${attrs.map(p => `<span>${esc(p)}</span>`).join('')}</div></div>`);
    }
    if (modDeps.length || resDeps.length) {
      extraBlocks.push(`
        <section class="workshop-detail-section">
          <h4>${esc(ctx.t('workshop.dependencies'))}</h4>
          <div class="workshop-dep-list">
            ${modDeps.map(d => depHtml(d, true)).join('')}
            ${resDeps.map(d => depHtml(d, false)).join('')}
          </div>
        </section>
      `);
    }
    if (credits.length) {
      extraBlocks.push(`
        <section class="workshop-detail-section">
          <h4>${esc(ctx.t('workshop.credits'))}</h4>
          <ul class="workshop-credit-list">
            ${credits
              .map(c => {
                const u = ctx.externalUrl(c.url);
                return `<li><strong>[${esc(c.role || 'other')}] ${esc(c.name || '')}</strong>${
                  u ? ` <a href="${esc(u)}" target="_blank" rel="noopener noreferrer">${esc(u)}</a>` : ''
                }${c.note ? `<p>${esc(c.note)}</p>` : ''}</li>`;
              })
              .join('')}
          </ul>
        </section>
      `);
    }

    return facts + extraBlocks.join('');
  }

  function renderDetailVersions(res, ctx) {
    const versions = Array.isArray(res.versions) ? res.versions : [];
    const latest = res.latestVersion || '';
    if (!versions.length) {
      return `<p class="workshop-panel-empty">${esc(ctx.t('workshop.no_versions'))}</p>`;
    }

    return versions
      .map(v => {
        const isLatest = v.version === latest;
        const changelogHtml = ctx.markdownHtml(`${res.id}:res:${v.version}`, v.changelog);
        const modDeps = Array.isArray(v.modDependencies) ? v.modDependencies : [];
        return `
          <div class="workshop-version-row">
            <div class="workshop-version-head">
              <strong>v${esc(v.version)}</strong>
              ${isLatest ? `<span class="workshop-mini-badge">${esc(ctx.t('workshop.latest'))}</span>` : ''}
              <span class="workshop-version-date">${ctx.formatDate(v.publishedAt)}</span>
              <span class="workshop-version-size">${ctx.formatBytes(v.sizeBytes)}</span>
              ${v.fileCount > 0 ? `<span class="workshop-version-size">${esc(ctx.t('workshop.file_count', { count: fmt(v.fileCount) }))}</span>` : ''}
              <span class="workshop-version-size">${esc(ctx.t('workshop.version_downloads', { count: fmt(v.downloads) }))}</span>
              <button class="secondary-btn workshop-version-install" data-install="${esc(res.id)}" data-version="${esc(v.version)}" type="button">${esc(ctx.t('workshop.install'))}</button>
            </div>
            ${changelogHtml ? `<div class="workshop-version-notes markdown-body">${changelogHtml}</div>` : `<p class="workshop-version-notes is-empty">${esc(ctx.t('workshop.no_changelog'))}</p>`}
            <div class="workshop-version-meta">
              ${v.gameVersion ? `<span>${esc(ctx.t('workshop.game_version'))} ${esc(v.gameVersion)}</span>` : ''}
              ${ctx.describePlatforms(v.platforms) ? `<span>${esc(ctx.describePlatforms(v.platforms))}</span>` : ''}
              ${modDeps.length ? `<span>${esc(ctx.t('workshop.dependencies'))} ${esc(modDeps.map(d => d.minVersion ? `${d.id} ≥ ${d.minVersion}` : d.id).join(' / '))}</span>` : ''}
            </div>
            <code class="workshop-sha" title="${esc(v.sha256)}">SHA-256 ${esc(v.sha256)}</code>
          </div>
        `;
      })
      .join('');
  }

  window.WorkshopResources = {
    formatSpeed,
    formatProgressDetail,
    actionForResource,
    renderCard,
    renderRow,
    renderDetailHero,
    renderDetailAction,
    renderDetailFacts,
    renderDetailVersions,
    openPreviewLightbox
  };
})();
