# Projetos-Grupo-5
Plataforma de movimento cyberpunk

## Smart Merge (obrigatório para quem mexe em cenas e prefabs)

Cenas (`.unity`), prefabs (`.prefab`) e assets (`.asset`) são YAML. O merge de texto do git
não entende esse formato e deixa marcadores `<<<<<<<` que corrompem a cena (foi o que aconteceu
com a antiga `QuartaFase.unity`). O `.gitattributes` já manda o git usar o **UnityYAMLMerge**
nesses arquivos, mas cada pessoa precisa registrar o driver **uma vez** no seu clone.

Rode na pasta do projeto (ajuste o caminho se a Unity não estiver no local padrão do Hub):

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.1.7f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -h -p --force --fallback none %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```

No macOS o executável fica em
`/Applications/Unity/Hub/Editor/6000.1.7f1/Unity.app/Contents/Tools/UnityYAMLMerge`.

Para conferir: `git config --get merge.unityyamlmerge.driver` deve mostrar o comando acima.

**Como se comporta:**
- Alterações em objetos ou campos diferentes da mesma cena são combinadas automaticamente,
  inclusive nos casos em que o merge de texto daria conflito.
- Se duas pessoas mudarem **o mesmo campo** do mesmo objeto, o git acusa conflito (`UU` no
  `git status`) e mostra no terminal quais campos conflitaram (`Left`/`Right`). O arquivo **não**
  recebe marcadores de texto: ele fica com um YAML válido usando o valor do branch que está
  entrando. Resolva com `git checkout --ours <arquivo>` / `git checkout --theirs <arquivo>`, ou
  abra a cena na Unity, acerte o valor e salve. Só então faça `git add`.
- `--fallback none` evita que a ferramenta tente abrir um programa gráfico de merge e trave o git.

**Mesmo assim:** avise o grupo antes de editar uma cena que outra pessoa pode estar editando.
