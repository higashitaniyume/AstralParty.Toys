// v2/credits.js — 设置页「感谢」分区（开发者与赞助者）
// 成员结构：{ name, avatar, note?, links?: [{ label, url? }] }
// - avatar 为 WebUI/images/credits/ 下内嵌本地路径；
// - note 为可选个人备注，留空时不显示；
// - links 里带 url 会渲染为外链按钮（由宿主在浏览器打开），不带 url 则显示为纯标签。
(function () {
  const AVATARS = {
    aperfish: 'images/credits/aperfish.webp',
    fengmiao: 'images/credits/fengmiao.webp',
    tenkinmori: 'images/credits/tenkinmori.webp',
    higashitaniyume: 'images/credits/higashitaniyume.webp',
    glyceryl6: 'images/credits/glyceryl6.webp',
    zhizhi: 'images/credits/zhizhi.webp',
    gujimy: 'images/credits/gujimy.webp',
    sean: 'images/credits/sean.webp',
    jhuli5254: 'images/credits/jhuli5254.webp',
    community: 'images/UT_AccountHeadShot_70902.webp',
  };

  const QQ_JOIN_URL = 'https://qm.qq.com/cgi-bin/qm/qr?k=tYrqev4U2Xr2PPAA4EkILA0NKmP3zgeU&jump_from=webapi&authKey=jHiIqsg0jEV53dBtQ9V3qXohKKHwLDGmfWprcrg7gueM9pU9iz+ST/QHR6d1j3wa';
  const DISCORD_URL = 'https://discord.gg/SZaYSwGX94';

  const PEOPLE = {
    aperfish: (note = '给 Windows 端加上了 C 盘瘦身、变速和更新提醒，还有给安卓端做出了丝滑悬浮窗和各种渠道服适配，还写了很多很多实用Mod咪～', badge = '') => ({
      name: '囚魚玖',
      avatar: AVATARS.aperfish,
      badge,
      note,
      links: [
        { platform: 'GitHub', label: '@Aperfish', url: 'https://github.com/Aperfish' },
        { platform: 'Bilibili', label: '@Aper_fish' },
      ],
    }),
    fengmiao: (note = '枫喵给 AstralParty.Toys 换了一个新皮肤！还塞了主页背景和 PVE 小面板，让工具箱内外都漂漂亮亮的咪～现在看到的新界面就是枫喵做的咪~ 而且还给企鹅咪开发工具箱给予了极大的帮助咪~') => ({
      name: '枫喵',
      avatar: AVATARS.fengmiao,
      note,
      links: [
        { platform: 'Bilibili', label: '@枫华璃67', url: 'https://space.bilibili.com/1943206718' },
      ],
    }),
    tenkinmori: (note = '把安卓悬浮窗打磨得又顺手又可爱咪，搞定了 Shizuku / Root 权限兼容，大家超喜欢的主页背景 Mod 原作也是他写的咪~ 而且还有他的很多技术支持咪~', badge = '') => ({
      name: 'TenkinMori',
      avatar: AVATARS.tenkinmori,
      badge,
      note,
      links: [
        { platform: 'GitHub', label: '@TenkinMori', url: 'https://github.com/TenkinMori' },
        { platform: 'Bilibili', label: '@TenkinMori' },
      ],
    }),
    higashitaniyume: (note = '') => ({
      name: 'higashitaniyume',
      avatar: AVATARS.higashitaniyume,
      note,
      links: [
        { platform: 'GitHub', label: '@higashitaniyume', url: 'https://github.com/higashitaniyume' },
      ],
    }),
    glyceryl6: (note = '开发了很多好用的Mod咪！', badge = '') => ({
      name: 'Glyceryl6',
      avatar: AVATARS.glyceryl6,
      badge,
      note,
      links: [
        { platform: 'GitHub', label: '@Glyceryl6', url: 'https://github.com/Glyceryl6' },
      ],
    }),
    zhizhi: (note = '', badge = '') => ({
      name: '知芝酱',
      avatar: AVATARS.zhizhi,
      badge,
      note,
      links: [
        { platform: 'GitHub', label: '@Ff1462118047', url: 'https://github.com/Ff1462118047' },
      ],
    }),
    sean: (note = '', badge = '') => ({
      name: '这就是上树（Sean）',
      avatar: AVATARS.sean,
      badge,
      note,
      links: [
        { platform: 'GitHub', label: '@climbtree' },
      ],
    }),
    gujimy: (note = '', badge = '') => ({
      name: 'laomo',
      avatar: AVATARS.gujimy,
      badge,
      note,
      links: [
        { platform: 'GitHub', label: '@gujimy', url: 'https://github.com/gujimy' },
      ],
    }),
    minasannomama: (note = '', badge = '') => ({
      name: '米娜桑的妈妈',
      avatar: '',
      badge,
      note,
      links: [],
    }),
    jhuli5254: (note = '帮我们修复了 CombatOddsMod 的好多问题，大幅优化了性能与生命周期清理咪～', badge = '') => ({
      name: 'jhuli5254-ship-it',
      avatar: AVATARS.jhuli5254,
      badge,
      note,
      links: [
        { platform: 'GitHub', label: '@jhuli5254-ship-it', url: 'https://github.com/jhuli5254-ship-it' },
      ],
    }),
  };

  const DEVELOPER_SECTIONS = [
    {
      id: 'community',
      title: 'QQ 群与 Discord 社区',
      subtitle: '感谢大家的帮助、建议与反馈',
      members: [
        {
          name: 'QQ 群 & Discord 频道的大家',
          avatar: AVATARS.community,
          note: '感谢 QQ 群和 Discord 频道里每一位朋友给予的帮助、建议、测试反馈与意见，让工具箱和加载器不断变好咪～',
          links: [
            { label: '加入 QQ 群', url: QQ_JOIN_URL },
            { label: '加入 Discord 频道', url: DISCORD_URL },
          ],
        },
      ],
    },
    {
      id: 'windows',
      title: 'AstralParty.Toys · Windows',
      subtitle: '主要开发者',
      repoUrl: 'https://github.com/higashitaniyume/AstralParty.Toys',
      repoLabel: 'higashitaniyume/AstralParty.Toys',
      members: [
        PEOPLE.fengmiao(),
        PEOPLE.aperfish(),
      ],
    },
    {
      id: 'android',
      title: 'AstralParty.Toys · 安卓端',
      subtitle: '主要开发者',
      repoUrl: 'https://github.com/aspatoys/AstralParty.Toys.Android',
      repoLabel: 'aspatoys/AstralParty.Toys.Android',
      members: [
        PEOPLE.tenkinmori(),
        PEOPLE.aperfish(),
      ],
    },
    {
      id: 'ios',
      title: 'AstralParty.Toys · iOS 端',
      subtitle: '主要开发者',
      repoUrl: 'https://github.com/aspatoys/AstralParty.Toys.iOS',
      repoLabel: 'aspatoys/AstralParty.Toys.iOS',
      members: [
        PEOPLE.higashitaniyume(),
      ],
    },
    {
      id: 'cesiumloader',
      title: 'CesiumLoader',
      subtitle: '主要开发者',
      repoUrl: 'https://github.com/higashitaniyume/CesiumLoader',
      repoLabel: 'higashitaniyume/CesiumLoader',
      members: [
        PEOPLE.aperfish(),
        PEOPLE.fengmiao(),
        PEOPLE.glyceryl6(),
        PEOPLE.jhuli5254(),
      ],
    },
    {
      id: 'forge',
      title: 'forge.hiynet.com · 模组创作者',
      subtitle: '',
      repoUrl: 'https://forge.hiynet.com',
      repoLabel: 'forge.hiynet.com',
      members: [
        PEOPLE.zhizhi('在 Forge 贡献了超多趣味模组（炫彩 ID、黑名单扩展、吉星双语字幕、星趴游戏箱、露露的复仇等），高产又好玩咪～'),
        { ...PEOPLE.glyceryl6('在 Forge 带来了自由镜头、怪物路线预览、星币不足自动跳过商店、Buff 显示和 UI 检查器等超实用辅助模组～'), name: '一路走来（Glyceryl6）' },
        PEOPLE.tenkinmori('在 Forge 带来了魔裁主题首页、队友大调查和吉星小镇修改器等很多很有用的很有意思的Mod咪～'),
        PEOPLE.sean('在 Forge 带来了抽卡模拟器、星怒的酒和自动最高难度等脑洞大开的模组咪～'),
        PEOPLE.aperfish('在 Forge 带来了战斗牌点数显示与文言（华夏）语言包咪～'),
        PEOPLE.gujimy('感谢在 Forge 参与模组创作与社区分享～'),
        PEOPLE.minasannomama('感谢在 Forge 参与模组创作与社区分享～'),
      ],
    },
  ];

  // 赞助者名单：{ name, avatar?, note?, links?: [{ platform?, label, url? }] }
  const SPONSORS = [];

  function renderLinks(links) {
    if (!Array.isArray(links) || links.length === 0) return '';
    const items = links.map(link => {
      const text = link.platform ? `${link.platform} ${link.label}` : link.label;
      if (link.url) {
        return `<a class="v2-credit-link" target="_blank" rel="noopener noreferrer" href="${esc(link.url)}" title="${esc(link.url)}">${esc(text)}</a>`;
      }
      return `<span class="v2-credit-handle">${esc(text)}</span>`;
    }).join('');
    return `<footer>${items}</footer>`;
  }

  function renderPerson(person) {
    const note = String(person.note || '').trim();
    const badge = String(person.badge || '').trim();
    const initial = esc(String(person.name || '?').slice(0, 1).toUpperCase());
    const media = person.avatar
      ? `<img class="v2-credit-avatar" src="${esc(person.avatar)}" alt="${esc(person.name)}" width="48" height="48" loading="lazy" decoding="async">`
      : `<span class="v2-credit-avatar v2-credit-avatar-fallback" aria-hidden="true">${initial}</span>`;
    return `<article class="v2-credit-card">
      ${media}
      <header>
        <b>${esc(person.name)}</b>${badge ? `<span class="v2-tag">${esc(badge)}</span>` : ''}
      </header>
      ${note ? `<p class="v2-credit-note">${esc(note)}</p>` : ''}
      ${renderLinks(person.links)}
    </article>`;
  }

  function renderSection(sec) {
    const I = window.V2UI?.ICON || {};
    const repoActions = sec.repoUrl
      ? `<span class="v2-credit-repo-actions">
          <a class="v2-btn ghost v2-credit-repo-btn" target="_blank" rel="noopener noreferrer" href="${esc(sec.repoUrl)}" title="${esc(sec.repoUrl)}">${I.ext || ''}${esc(sec.repoLabel)}</a>
          <button type="button" class="v2-icon-btn" data-v2g="copyUrl" data-url="${esc(sec.repoUrl)}" title="复制仓库链接" aria-label="复制 ${esc(sec.title)} 仓库链接">${I.copy || ''}</button>
        </span>`
      : '';
    return `<section class="v2-card v2-credits-section">
      <div class="v2-card-title">
        <span>${esc(sec.title)} <span class="v2-muted v2-credit-sub">${esc(sec.subtitle || '')}</span></span>
        ${repoActions}
      </div>
      <div class="v2-credits-grid">
        ${sec.members.map(renderPerson).join('')}
      </div>
    </section>`;
  }

  function renderSponsors() {
    if (!SPONSORS.length) return '';
    return `<section class="v2-card v2-credits-section">
      <div class="v2-card-title"><span>赞助者 <span class="v2-muted v2-credit-sub">感谢支持项目维护与服务器开销</span></span></div>
      <div class="v2-credits-grid">${SPONSORS.map(renderPerson).join('')}</div>
    </section>`;
  }

  function render() {
    return `<div class="v2-credits-stack">
      <section class="v2-card v2-credits-intro">
        <div class="v2-card-title"><span>鸣谢开发者与赞助者</span></div>
        <p class="v2-muted v2-credits-lead">感谢为 AstralParty.Toys 全平台工具链、加载器与社区建设付出时间的每一位开发者与赞助者。</p>
      </section>
      ${DEVELOPER_SECTIONS.map(renderSection).join('')}
      ${renderSponsors()}
    </div>`;
  }

  window.V2Credits = {
    render,
    sections: DEVELOPER_SECTIONS,
    sponsors: SPONSORS,
  };
})();
