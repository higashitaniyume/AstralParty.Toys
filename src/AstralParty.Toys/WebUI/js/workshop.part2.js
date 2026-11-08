        button.textContent = action.label;
        button.disabled = true;
        button.removeAttribute('data-install');
        button.removeAttribute('data-version');
      } else {
        button.className = 'primary-btn workshop-install-btn';
        button.textContent = action.label;
        button.disabled = false;
        button.setAttribute('data-install', item.id);
        button.setAttribute('data-version', action.version);
      }
    }

    if (bar) {
      bar.hidden = percent === null;
      const fill = bar.firstElementChild;
      if (fill) fill.style.width = `${percent ?? 0}%`;
    }
  }

  function renderGrid() {
    const grid = $('workshopGrid');
    if (!grid) return;

    const isList = state.viewMode === 'list';
    grid.classList.toggle('is-list-view', isList);

    const filtered = filterItems(state.items);

    if (!filtered.length) {
      if (state.loading) {
        // 骨架占位：不等数据回来就先画骨架，UI 线程只做一次轻量 DOM 写入
        const skel = isList
          ? Array.from({ length: 6 }, () => `<div class="workshop-row" aria-hidden="true"><div class="v2-skel" style="width:56px;height:56px;border-radius:8px;flex:none"></div><div style="flex:1;display:grid;gap:6px"><div class="v2-skel v2-skel-block" style="width:40%"></div><div class="v2-skel v2-skel-block" style="width:70%"></div></div><div class="v2-skel" style="width:84px;height:30px;border-radius:8px"></div></div>`).join('')
          : Array.from({ length: 6 }, () => `<div class="v2-skel-card" aria-hidden="true"><div class="v2-skel v2-skel-thumb"></div><div class="v2-skel-body"><div class="v2-skel v2-skel-block" style="width:65%"></div><div class="v2-skel v2-skel-block" style="width:90%"></div></div></div>`).join('');
        grid.innerHTML = skel;
        return;
      }
      grid.innerHTML = `<div class="workshop-empty"><span class="mi" data-icon="extension"></span><p>${esc(
            t('workshop.empty')
          )}</p><button class="secondary-btn" data-retry="1">${esc(t('workshop.retry'))}</button></div>`;
      window.Icons?.hydrate(grid);
      return;
    }

    grid.innerHTML = isList
      ? filtered.map(rowMarkup).join('')
      : filtered.map(cardMarkup).join('');
    syncCards();
    window.Icons?.hydrate(grid);
    window.StickerModule?.syncAfterRender();
  }

  // 按当前安装状态刷新所有卡片的按钮/进度条（不重建节点）。
  function syncCards() {
    const grid = $('workshopGrid');
    if (!grid) return;
    grid.querySelectorAll('.workshop-card, .workshop-row').forEach(card => {
      const id = card.getAttribute('data-mod-id');
      const item = state.items.find(entry => entry.id === id);
      if (item) applyActionState(card, item);
    });
  }

  function renderPager() {
    const pager = $('workshopPager');
    if (!pager) return;
    const show = state.pages > 1;
    pager.classList.toggle('hidden', !show);
    if (!show) return;

    const info = $('workshopPageInfo');
    if (info) info.textContent = t('workshop.page_info', { page: state.page, pages: state.pages });
    const prev = $('workshopPrevBtn');
    const next = $('workshopNextBtn');
    if (prev) prev.disabled = state.page <= 1;
    if (next) next.disabled = state.page >= state.pages;
  }

  function syncTagOptions() {
    const select = $('workshopTagSelect');
    if (!select) return;
    const current = state.tag;
    select.innerHTML =
      `<option value="">${esc(t('workshop.tags_all'))}</option>` +
      state.tags.map(tag => `<option value="${esc(tag)}">${esc(tag)}</option>`).join('');
    select.value = current;
  }

  function syncSource() {
    const node = $('workshopSourceUrl');
    if (!node) return;
    const base = state.baseUrl || AppState.librarySettings?.workshopUrl || 'https://forge.hiynet.com';
    node.textContent = base;
    node.title = base;
  }

  function load(page) {
    if (state.loading) {
      state.pendingPage = Math.max(1, page || 1);
      return;
    }
    state.loading = true;
    state.page = Math.max(1, page || 1);
    hideNotice();
    renderGrid();
    syncSource();

    post({
      type: 'workshopList',
      query: state.query,
      tag: state.tag,
      sort: state.sort,
      page: state.page
    });
  }

  // ============================== 模组详情页 ==============================

  function skipMarkdown(markdown) {
    return String(markdown || '').trim();
  }

  /** README / 更新说明按模组缓存渲染结果，切 tab 时不重复解析。 */
  function markdownHtml(key, markdown) {
    if (!skipMarkdown(markdown)) return '';

    const cached = state.mdCache.get(key);
    if (cached !== undefined) return cached;

    let html = '';
    try {
      html = window.Markdown?.render(markdown) || '';
    } catch {
      html = `<pre class="workshop-md-fallback">${esc(markdown)}</pre>`;
    }

    state.mdCache.set(key, html);
    return html;
  }

  /** 市场数据不可全信：只有 http(s) 才允许变成可点击链接。 */
  function externalUrl(value) {
    const text = String(value || '').trim();
    return /^https?:\/\//i.test(text) ? text : '';
  }

  function openDetail(id, tab) {
    if (state.consent !== 'granted') {
      openConsentModal();
      return;
    }
    if (!id) return;

    const listScroll = document.querySelector('#workshopView .workshop-scroll');
    state.listScrollTop = listScroll ? listScroll.scrollTop : 0;
    state.detailId = id;
    state.detailTab = tab || 'details';
    state.detailError = '';

    const cached = state.details.get(id);
    state.detail = cached || null;

    showPage('workshopDetail');
    renderDetailPage();

    if (!cached) post({ type: 'workshopDetail', id });
  }

  // 网页点 aspatoys://install-mod 唤起：只打开详情页，由用户在详情页确认安装。
  // 指定了版本时直接停在「版本」页签，方便找到那个版本的安装按钮；没指定就停在「详情」。
  function openExternalModRequest(payload) {
    const id = String(payload?.id || '').trim();
    const version = String(payload?.version || '').trim();
    if (!id) {
      toast('这个 aspatoys 链接里没有模组 ID，打不开模组详情。');
      return;
    }
    state.externalMod = { id, version };

    if (state.consent === 'granted') {
      openExternalModNow();
      return;
    }
    if (state.consent === 'required') {
      openConsentModal();
      return;
    }
    // 还不知道同意状态：拉一次列表探一下，宿主会回列表（已同意）或同意要求（未同意），
    // 两种回复都会续跑下面的详情打开。
    load(1);
  }

  function openExternalModNow() {
    const pending = state.externalMod;
    if (!pending?.id || state.consent !== 'granted') return;
    state.externalMod = null;
    openDetail(pending.id, pending.version ? 'versions' : 'details');
  }

  function backToList() {
    showPage('workshop');
  }

  function requestDetail(id) {
    state.detailError = '';
    state.detailId = id;
    state.detail = null;
    renderDetailPage();
    post({ type: 'workshopDetail', id });
  }

  function detailActionMarkup(mod) {
    const installing = state.installingId === mod.id;
    const action = actionFor(mod);
    const latest = mod.latestVersion || '';

    if (mod.builtIn) {
      return `<span class="workshop-chip is-builtin">${esc(t('workshop.builtin'))}</span>`;
    }
    if (installing) {
      return `<button class="secondary-btn is-busy" id="workshopDetailCancelBtn" type="button">${esc(
        t('workshop.cancel')
      )}</button>`;
    }
    if (action.disabled) {
      return `<button class="secondary-btn" type="button" disabled>${esc(action.label)}</button>`;
    }
    return `<button class="primary-btn" type="button" data-install="${esc(mod.id)}" data-version="${esc(
      latest
    )}">${esc(action.label)}</button>`;
  }

  function detailFacts(mod) {
    const latest = (mod.versions || []).find(version => version.version === mod.latestVersion);
    const dependencies = Array.isArray(latest?.dependencies) ? latest.dependencies : [];
    const permissions = describePermissions(latest?.permissions || 0);

    const fact = (label, value, extra) =>
      `<div class="workshop-fact"><dt>${esc(label)}</dt><dd${
        extra ? ` class="${extra}"` : ''
      }>${value}</dd></div>`;

    const repositoryUrl = externalUrl(mod.repositoryUrl);
    const repository = repositoryUrl
      ? `<a href="${esc(repositoryUrl)}" target="_blank" rel="noopener noreferrer" title="${esc(
          repositoryUrl
        )}">${esc(repositoryUrl)}</a>`
      : esc(t('workshop.not_provided'));

    return `
      <dl class="workshop-facts">
        ${fact(t('workshop.mod_id'), esc(mod.id), 'is-mono')}
        ${fact(t('workshop.author'), esc(mod.author || t('workshop.anonymous')))}
        ${fact(t('workshop.latest'), latest ? `v${esc(latest.version)}` : '—', 'is-mono')}
        ${fact(t('workshop.package_size'), latest ? formatBytes(latest.sizeBytes) : '—')}
        ${fact(t('workshop.downloads'), fmt(mod.downloads))}
        ${fact(t('workshop.updated'), formatDate(mod.updatedAt))}
        ${fact(t('workshop.repository'), repository)}
        ${fact(
          t('workshop.license'),
          esc(mod.license || t('workshop.license_none')),
          mod.license ? 'is-mono' : ''
        )}
        ${latest ? fact(t('workshop.sdk'), esc(latest.sdkVersion || t('workshop.undeclared'))) : ''}
        ${
          permissions.length
            ? fact(t('workshop.permissions'), esc(permissions.join(' / ')))
            : ''
        }
        ${
          dependencies.length
            ? fact(
                t('workshop.dependencies'),
                esc(
                  dependencies
                    .map(dependency =>
                      dependency.minVersion
                        ? `${dependency.id} ≥ ${dependency.minVersion}`
                        : dependency.id
                    )
                    .join(' / ')
                )
              )
            : ''
        }
        ${fact(
          t('workshop.hosting'),
          esc(latest?.hosted ? t('workshop.hosted_by_market') : t('workshop.hosted_by_author'))
        )}
      </dl>
    `;
  }

  function detailDownloadBlock(mod) {
    const latest = (mod.versions || []).find(version => version.version === mod.latestVersion);
    if (!latest) return '';

    const sourceUrl = externalUrl(latest.sourceUrl);
    const source = latest.hosted || !sourceUrl
      ? `<span class="workshop-download-note">${esc(t('workshop.hosted_by_market_hint'))}</span>`
      : `<a class="workshop-download-link" href="${esc(sourceUrl)}" target="_blank" rel="noopener noreferrer" title="${esc(
          sourceUrl
        )}">${esc(sourceUrl)}</a>`;

    return `
      <section class="workshop-detail-section">
        <h4>${esc(t('workshop.download_address'))}</h4>
        <div class="workshop-download-row">${source}</div>
        <code class="workshop-sha" title="${esc(latest.sha256)}">SHA-256 ${esc(latest.sha256)}</code>
      </section>
    `;
  }

  function detailDescriptionBlock(mod) {
    return `
      <section class="workshop-detail-section">
        <h4>${esc(t('workshop.description'))}</h4>
        <p class="workshop-detail-text">${esc(mod.description || t('workshop.no_description'))}</p>
      </section>
    `;
  }

  function detailVersionNotes(version) {
    const html = markdownHtml(`${state.detailId}:${version.version}`, version.changelog);
    return html
      ? `<div class="workshop-version-notes markdown-body">${html}</div>`
      : `<p class="workshop-version-notes is-empty">${esc(t('workshop.no_changelog'))}</p>`;
  }

  function detailVersionsPanel(mod) {
    const versions = Array.isArray(mod.versions) ? mod.versions : [];
    const latest = mod.latestVersion || '';
    const canInstall = !mod.builtIn;

    if (!versions.length) {
      return `<p class="workshop-panel-empty">${esc(t('workshop.no_versions'))}</p>`;
    }

    return versions
      .map(version => {
        const permissions = describePermissions(version.permissions);
        const dependencies = Array.isArray(version.dependencies) ? version.dependencies : [];
        const isLatest = version.version === latest;

        return `
          <div class="workshop-version-row">
            <div class="workshop-version-head">
              <strong>v${esc(version.version)}</strong>
              ${isLatest ? `<span class="workshop-mini-badge">${esc(t('workshop.latest'))}</span>` : ''}
              <span class="workshop-version-date">${formatDate(version.publishedAt)}</span>
              <span class="workshop-version-size">${formatBytes(version.sizeBytes)}</span>
              <span class="workshop-version-size">${esc(
                t('workshop.version_downloads', { count: fmt(version.downloads) })
              )}</span>
              ${
                canInstall
                  ? `<button class="secondary-btn workshop-version-install" data-install="${esc(
                      mod.id
                    )}" data-version="${esc(version.version)}" type="button">${esc(
                      t('workshop.install')
                    )}</button>`
                  : ''
              }
            </div>
            ${detailVersionNotes(version)}
            <div class="workshop-version-meta">
              <span>${esc(t('workshop.sdk'))} ${esc(version.sdkVersion || t('workshop.undeclared'))}</span>
              ${
                permissions.length
                  ? `<span>${esc(t('workshop.permissions'))} ${esc(permissions.join(' / '))}</span>`
                  : ''
              }
              <span>${esc(t('workshop.dependencies'))} ${
                dependencies.length
                  ? esc(
                      dependencies
                        .map(dependency =>
                          dependency.minVersion
                            ? `${dependency.id} ≥ ${dependency.minVersion}`
                            : dependency.id
                        )
                        .join(' / ')
                    )
                  : esc(t('workshop.no_dependencies'))
              }</span>
            </div>
            <code class="workshop-sha" title="${esc(version.sha256)}">SHA-256 ${esc(
              version.sha256
            )}</code>
          </div>
        `;
      })
      .join('');
  }

  function detailReadmePanel(mod) {
    const html = markdownHtml(`${state.detailId}:readme`, mod.readme);
    return html
      ? `<div class="workshop-readme markdown-body">${html}</div>`
      : `<p class="workshop-panel-empty">${esc(t('workshop.no_readme'))}</p>`;
  }

  function detailLicensePanel(mod) {
    return `
      <div class="workshop-detail-section">
        <div class="workshop-license-head">
          <h4>${esc(t('workshop.license'))}</h4>
          <span class="workshop-license-tag">${esc(mod.license || t('workshop.license_none'))}</span>
        </div>
        ${
          mod.licenseText
            ? `<pre class="workshop-license-text">${esc(mod.licenseText)}</pre>`
            : `<p class="workshop-panel-empty">${esc(t('workshop.no_license'))}</p>`
        }
      </div>
    `;
  }

  function detailTabsMarkup(mod) {
    const versions = Array.isArray(mod.versions) ? mod.versions.length : 0;
    const tabs = [
      ['details', t('workshop.tab_details')],
      ['versions', t('workshop.tab_versions', { count: versions })],
      ['readme', t('workshop.tab_readme')],
      ['license', t('workshop.tab_license')]
    ];

    const buttons = tabs
      .map(([id, label]) => {
        const active = state.detailTab === id;
