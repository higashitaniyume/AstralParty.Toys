# 第三方前端资源

市场详情页的 README / 更新说明按 GitHub 的风格渲染。这些文件原样取自各自的 npm 包，
随 WebUI 一起内嵌进程序集（`AstralParty.Toys.csproj` 的 `WebUI\**\*` 通配）。

| 文件 | 来源 | 版本 | 许可证 |
| --- | --- | --- | --- |
| `marked.umd.js` | [`marked`](https://www.npmjs.com/package/marked) `lib/marked.umd.js` | 18.1.2 | MIT（`LICENSE-marked.md`） |
| `purify.min.js` | [`dompurify`](https://www.npmjs.com/package/dompurify) `dist/purify.min.js` | 3.4.17 | Apache-2.0 OR MPL-2.0（`LICENSE-dompurify.txt`） |
| `highlight.min.js` | [`@highlightjs/cdn-assets`](https://www.npmjs.com/package/@highlightjs/cdn-assets) 浏览器构建 | 11.12.0 | BSD-3-Clause（`LICENSE-highlightjs.txt`） |
| `highlight-github-dark.min.css` | 同上的 `styles/github-dark.min.css` | 11.12.0 | BSD-3-Clause |
| `github-markdown-dark.css` | [`github-markdown-css`](https://www.npmjs.com/package/github-markdown-css) | 5.9.0 | MIT（`LICENSE-github-markdown-css.txt`） |

升级步骤：从 jsDelivr 取对应版本的文件覆盖同名文件，更新上表版本号，然后在 WebView2 里
打开一个带 README 的模组检查排版（代码块高亮、表格、任务列表）。

`js/markdown.js` 是本站自己的胶水代码：配置 marked、用 DOMPurify 消毒、只放行 http(s)
链接与图片，并把链接交给系统浏览器打开。
