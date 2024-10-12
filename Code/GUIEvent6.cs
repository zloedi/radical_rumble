using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RR {
    

using Cl = RR.Client;

using static QUI.WidgetResult;


public static class GuiEvent6 {

static readonly string _tblCrashes = @"""Day of Week"",""Number of Crashes""
""Sunday"",13664
""Monday"",17279
""Tuesday"",17337
""Wednesday"",17394
""Thursday"",17954
""Friday"",19147
""Saturday"",15714
";

static List<string[]> _table;
static WrapBox _wboxWindow = new WrapBox{ id = -1 };
static bool _closed;

public static void Tick_ui( WrapBox wbox ) {
    if ( _table == null ) {
        CsvReader reader = new();
        _table = new( reader.Read( _tblCrashes ) );
    }

    WrapBox.DisableCanvasScale();

    int fontSize = 14;
    int textGap = fontSize / 2;
    int border = 4;
    int cellW = fontSize * 12;
    int cellH = fontSize * 2;
    int titleH = 32;
    int gap = 1;
    Color colFrame = new Color( 0.7f, 0.7f, 0.7f );
    Color colTitlebar = new Color( 0.1f, 0.1f, 1f );

    var wbTable = wbox.TopLeft( _table.Count > 0 ? _table[0].Length * cellW + gap : 100,
                                Mathf.Max( 1, _table.Count ) * cellH + gap, y: 300 );
    if ( _closed ) {
        wbTable = wbox.TopLeft( wbTable.W, 0 );
    }

    if ( _wboxWindow.w != wbox.w || _wboxWindow.h != wbox.h ) {
        _wboxWindow = wbox;
    }
        
    wbox = _wboxWindow;

    wbox = wbox.Center( wbTable.W, wbTable.H );
    wbox = wbox.TopLeft( wbTable.W, wbTable.H + titleH );

    // black outline
    WBUI.FillRect( wbox.Center( wbox.W + ( border + gap ) * 2, wbox.H + ( border + gap ) * 2 ),
                                                                                    Color.black );

    // frame
    WBUI.FillRect( wbox.Center( wbox.W + border * 2, wbox.H + border * 2 ), colFrame );

    // titlebar
    var wbTitle = wbox.TopLeft( wbox.W, titleH - border );
    WBUI.FillRect( wbTitle, colTitlebar );
    var res = WBUI.ClickRect( wbTitle );
    if ( res == Active ) {
        QUI.DragPosition( res, ref _wboxWindow.x, ref _wboxWindow.y );
    }

    // close button
    var wbClose = wbTitle.CenterRight( wbTitle.H - border * 2, wbTitle.H - border * 2, x: border );
    WBUI.FillRect( wbClose.Center( wbClose.W + gap * 2, wbClose.H + gap * 2 ), Color.black );
    WBUI.FillRect( wbClose, colFrame );
    WBUI.Text( _closed ? " ^" : " x", wbClose, fontSize: 18, color: Color.black );
    res = WBUI.ClickRect( wbClose );
    if ( res == Pressed ) {
        _closed = ! _closed;
    }

    // title text
    wbTitle = wbTitle.Center( wbTitle.W - textGap, wbTitle.H - textGap );
    WBUI.Text( "Table of crashes", wbTitle, font: GUIUnity.font, fontSize: fontSize,
                                                                            color: Color.white );
    if ( ! _closed ) {
        wbox = wbox.BottomLeft( wbox.W, wbTable.H );
        WBUI.FillRect( wbox, Color.black );
        for ( int i = 0; i < _table.Count; i++ ) {
            string [] row = _table[i];
            for ( int j = 0; j < row.Length; j++ ) {
                var wbCell = wbox.TopLeft( cellW - gap, cellH - gap,
                                                x: cellW * j + gap, y: cellH * i + gap,
                                                id: ( i << 16 | j ) );
                WBUI.FillRect( wbCell, Color.white );
                wbCell = wbCell.Center( wbCell.W - textGap, wbCell.H - textGap );
                WBUI.Text( row[j], wbCell, font: GUIUnity.font, fontSize: fontSize, color: Color.black );
            }
        }
    }

    WrapBox.EnableCanvasScale();


    //QGL.LatePrint( $"{Cl.mousePosScreen}\n{new Vector2( WrapBox.Unscale( Cl.mousePosScreen.x ), WrapBox.Unscale( Cl.mousePosScreen.y ) )}", Cl.mousePosScreen, Color.white );
}

static void Parse() {
}

} // GuiEvent6

public class CsvConfig
{
    public char Delimiter { get; private set; }
    public string NewLineMark { get; private set; }
    public char QuotationMark { get; private set; }
 
    public CsvConfig(char delimiter, string newLineMark, char quotationMark)
    {
        Delimiter = delimiter;
        NewLineMark = newLineMark;
        QuotationMark = quotationMark;
    }
 
    // useful configs
 
    public static CsvConfig Default
    {
        get { return new CsvConfig(',', "\r\n", '\"'); }
    }
 
    // etc.
}

public class CsvReader
{
    private CsvConfig m_config;
 
    public CsvReader(CsvConfig config = null)
    {
        if (config == null)
            m_config = CsvConfig.Default;
        else
            m_config = config;
    }
 
    public List<string[]> Read(string csvFileContents)
    {
        var result = new List<string[]>();
        using (StringReader reader = new StringReader(csvFileContents))
        {
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null)
                    break;
                result.Add(ParseLine(line));
            }
        }
        return result;
    }
 
    private string[] ParseLine(string line)
    {
        Stack<string> result = new Stack<string>();
 
        int i = 0;
        while (true)
        {
            string cell = ParseNextCell(line, ref i);
            if (cell == null)
                break;
            result.Push(cell);
        }
 
        // remove last elements if they're empty
        while (string.IsNullOrEmpty(result.Peek()))
        {
            result.Pop();
        }
 
        var resultAsArray = result.ToArray();
        Array.Reverse(resultAsArray);
        return resultAsArray;
    }
 
    // returns iterator after delimiter or after end of string
    private string ParseNextCell(string line, ref int i)
    {
        if (i >= line.Length)
            return null;
 
        if (line[i] != m_config.QuotationMark)
            return ParseNotEscapedCell(line, ref i);
        else
            return ParseEscapedCell(line, ref i);
    }
 
    // returns iterator after delimiter or after end of string
    private string ParseNotEscapedCell(string line, ref int i)
    {
        StringBuilder sb = new StringBuilder();
        while (true)
        {
            if (i >= line.Length) // return iterator after end of string
                break;
            if (line[i] == m_config.Delimiter)
            {
                i++; // return iterator after delimiter
                break;
            }
            sb.Append(line[i]);
            i++;
        }
        return sb.ToString();
    }
 
    // returns iterator after delimiter or after end of string
    private string ParseEscapedCell(string line, ref int i)
    {
        i++; // omit first character (quotation mark)
        StringBuilder sb = new StringBuilder();
        while (true)
        {
            if (i >= line.Length)
                break;
            if (line[i] == m_config.QuotationMark)
            {
                i++; // we're more interested in the next character
                if (i >= line.Length)
                {
                    // quotation mark was closing cell;
                    // return iterator after end of string
                    break;
                }
                if (line[i] == m_config.Delimiter)
                {
                    // quotation mark was closing cell;
                    // return iterator after delimiter
                    i++;
                    break;
                }
                if (line[i] == m_config.QuotationMark)
                {
                    // it was doubled (escaped) quotation mark;
                    // do nothing -- we've already skipped first quotation mark
                }
 
            }
            sb.Append(line[i]);
            i++;
        }
 
        return sb.ToString();
    }
} // CsvReader

} // RR
