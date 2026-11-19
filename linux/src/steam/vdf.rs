use std::borrow::Cow;

type Entry<'a> = (Cow<'a, str>, Value<'a>);

pub(super) struct Object<'a>(pub Vec<Entry<'a>>);

pub(super) enum Value<'a> {
    Text(Cow<'a, str>),
    Object(Object<'a>),
}

impl<'a> Object<'a> {
    fn get(&self, key: &str) -> Result<Option<&Value<'a>>, String> {
        let mut matches = self.0.iter().filter(|(name, _)| name.eq_ignore_ascii_case(key));
        let found = matches.next();
        if matches.next().is_some() {
            return Err(format!("Steam VDF 存在重复字段 {key}"));
        }
        Ok(found.map(|(_, value)| value))
    }

    pub fn object(&self, key: &str) -> Result<Option<&Self>, String> {
        match self.get(key)? {
            Some(Value::Object(object)) => Ok(Some(object)),
            None => Ok(None),
            _ => Err(format!("Steam VDF 字段 {key} 必须是对象")),
        }
    }

    pub fn text(&self, key: &str) -> Result<Option<&str>, String> {
        match self.get(key)? {
            Some(Value::Text(text)) => Ok(Some(text)),
            None => Ok(None),
            _ => Err(format!("Steam VDF 字段 {key} 必须是字符串")),
        }
    }

    pub fn contains(&self, key: &str) -> Result<bool, String> {
        Ok(self.get(key)?.is_some())
    }
}

pub(super) fn parse(input: &str) -> Result<Object<'_>, String> {
    Parser {
        input: input.strip_prefix('\u{feff}').unwrap_or(input),
        offset: 0,
    }
    .object(0)
}

struct Parser<'a> {
    input: &'a str,
    offset: usize,
}

impl<'a> Parser<'a> {
    fn skip_space(&mut self) {
        loop {
            let tail = &self.input[self.offset..];
            self.offset += tail.len() - tail.trim_start_matches(|c: char| c.is_ascii_whitespace()).len();
            let tail = &self.input[self.offset..];
            if !tail.starts_with("//") {
                return;
            }
            self.offset += tail.find('\n').unwrap_or(tail.len());
        }
    }

    fn byte(&self) -> Option<u8> {
        self.input.as_bytes().get(self.offset).copied()
    }

    fn error(&self, why: &str) -> String {
        format!("Steam VDF 无效（字节 {}）：{why}", self.offset + 1)
    }

    fn text(&mut self) -> Result<Cow<'a, str>, String> {
        if self.byte() != Some(b'"') {
            let start = self.offset;
            while let Some(byte) = self.byte() {
                if byte.is_ascii_whitespace() || matches!(byte, b'{' | b'}' | b'"' | 0) {
                    break;
                }
                self.offset += 1;
            }
            if self.offset == start {
                return Err(self.error("缺少键或值"));
            }
            return Ok(Cow::Borrowed(&self.input[start..self.offset]));
        }
        self.offset += 1;
        let mut start = self.offset;
        let mut decoded: Option<String> = None;
        while let Some(byte) = self.byte() {
            match byte {
                b'"' => {
                    let tail = &self.input[start..self.offset];
                    self.offset += 1;
                    return Ok(match decoded {
                        None => Cow::Borrowed(tail),
                        Some(mut value) => {
                            value.push_str(tail);
                            Cow::Owned(value)
                        }
                    });
                }
                b'\\' => {
                    let escaped = match self.input.as_bytes().get(self.offset + 1) {
                        Some(b'\\') => Some('\\'),
                        Some(b'"') => Some('"'),
                        Some(b'n') => Some('\n'),
                        Some(b'r') => Some('\r'),
                        Some(b't') => Some('\t'),
                        _ => None,
                    };
                    if let Some(escaped) = escaped {
                        let output = decoded.get_or_insert_with(String::new);
                        output.push_str(&self.input[start..self.offset]);
                        output.push(escaped);
                        self.offset += 2;
                        start = self.offset;
                        continue;
                    }
                }
                0 => return Err(self.error("字符串包含 NUL")),
                _ => (),
            }
            self.offset += 1;
        }
        Err(self.error("字符串没有结束引号"))
    }

    fn object(&mut self, depth: usize) -> Result<Object<'a>, String> {
        if depth > 64 {
            return Err(self.error("对象嵌套过深"));
        }
        let mut entries = Vec::new();
        loop {
            self.skip_space();
            match (self.byte(), depth) {
                (None, 0) => return Ok(Object(entries)),
                (Some(b'}'), 1..) => {
                    self.offset += 1;
                    return Ok(Object(entries));
                }
                _ => (),
            }
            let key = self.text()?;
            self.skip_space();
            let value = if self.byte() == Some(b'{') {
                self.offset += 1;
                Value::Object(self.object(depth + 1)?)
            } else {
                Value::Text(self.text()?)
            };
            entries.push((key, value));
        }
    }
}

#[cfg(test)]
mod tests {
    use super::parse;

    #[test]
    fn supports_comments_bom_multiline_and_escaped_paths() {
        let source = concat!("\u{feff}", r#"
            // Steam can emit arbitrary whitespace and comments.
            LibraryFolders {
                0 { path "/media/Steam \"Drive\"" }
                1 "/media/Steam\\Library"
                multiline "comma,
line"
            }
        "#);
        let tree = parse(source).unwrap();
        let libraries = tree.object("libraryfolders").unwrap().unwrap();
        assert_eq!(libraries.object("0").unwrap().unwrap().text("path").unwrap(), Some("/media/Steam \"Drive\""));
        assert_eq!(libraries.text("1").unwrap(), Some("/media/Steam\\Library"));
        assert_eq!(libraries.text("multiline").unwrap(), Some("comma,\nline"));
    }

    #[test]
    fn rejects_truncation_unbalanced_braces_and_ambiguous_keys() {
        for input in ["\"unfinished", "key { child value", "key value }", "key"] {
            assert!(parse(input).is_err(), "{input}");
        }
        let tree = parse(r#""appid" "2622000" "AppID" "other""#).unwrap();
        assert!(tree.text("appid").is_err());
    }
}
