# Domain Docs

工程技能在探索代码库时，如何消费本仓库的领域文档。

## 探索前先读这些

- 根目录的 **`GLOSSARY.md`**，或
- 若存在根目录 **`GLOSSARY-MAP.md`**：它指向每个上下文各自的 `GLOSSARY.md`，读与当前话题相关的那几份。
- **`docs/adr/`**：读与你要动的区域相关的 ADR。多上下文仓库还要看 `src/<context>/docs/adr/`。

这些文件不存在就**静默继续**。不要提示缺失，也不要主动建议先建。`/domain-modeling` 技能（经 `/grill-with-docs`、`/improve-codebase-architecture` 进入）会在术语或决策真正落定的那一刻按需创建它们。

## 文件结构

单上下文仓库（本仓库）：

```
/
├── GLOSSARY.md
├── docs/adr/
│   ├── 0001-plasticscm-authoritative-history.md
│   └── 0002-...
└── src/
```

多上下文仓库（根目录存在 `GLOSSARY-MAP.md` 时）：

```
/
├── GLOSSARY-MAP.md
├── docs/adr/                          ← 跨上下文的决策
└── src/
    ├── ordering/
    │   ├── GLOSSARY.md
    │   └── docs/adr/                  ← 上下文内决策
    └── billing/
        ├── GLOSSARY.md
        └── docs/adr/
```

## 用词汇表里的说法

输出里提到领域概念时（issue 标题、重构提案、假设、测试名），用 `GLOSSARY.md` 里定义的术语。不要换成词汇表明确回避的同义词。

如果需要的概念还不在词汇表里，这是个信号：要么你在自造项目并不用的语言（重新考虑），要么确实有空缺（记下来交给 `/domain-modeling`）。

## 标出 ADR 冲突

输出与既有 ADR 矛盾时，明确点出来，不要默默覆盖：

> _Contradicts ADR-0007 (event-sourced orders), but worth reopening because…_
