export default class RangeInput{
    headers: string[];
    nHeaders : number;
    nRows : number;
    dataByHeader : Map<string, any[]>; 
    dataByRow : Map<string, any>[];
    
    constructor(range : any[][]) {
        const headerRow = range[0];
        this.headers = [];
        for (const v of headerRow)
            this.headers.push(v);    
        
        this.nHeaders = this.headers.length;
        
        this.nRows = range.length;
        console.log(`Rows : ${this.nRows}`);
        console.log(`Columns : ${this.nHeaders}`);
        this.dataByHeader = new Map<string, any[]>();
        for (var iHeader = 0; iHeader < this.nHeaders; iHeader++)
        {
            var headerData : any[] = [];
            for (var i = 1; i < this.nRows; i++)
                headerData.push(range[i][iHeader]);
            
            this.dataByHeader.set(this.headers[iHeader], headerData);
        }
        
        this.dataByRow = [];
        for (var i = 0; i < this.nRows; i++)
        {
            var byRow : Map<string, any> = new Map<string, any>();
            for (var j = 0; j < this.nHeaders; j++)
                byRow.set(this.headers[j], range[i][j]);
            this.dataByRow.push(byRow);
        }
    }
    
    public getByHeader( header : string ) : any[] | undefined {
        if (this.headers.indexOf(header) >= 0) {
            return this.dataByHeader.get(header);
        }
        console.log(`${header} not found in ${this.headers.join(",")}`);
        return undefined;
    }
     
    public getByRow(i : number) : Map<string, any> | undefined {
        if ( i >= this.nRows )
            return undefined;
        
        return this.dataByRow[i];
    } 
}