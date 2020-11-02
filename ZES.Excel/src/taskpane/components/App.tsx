import * as React from 'react';
import { Button, ButtonType } from 'office-ui-fabric-react';
import Header from './Header';
import HeroList, { HeroListItem } from './HeroList';
import Progress from './Progress';
import { request } from 'graphql-request';
import RangeInput from "./RangeInput";

declare global {
  interface Window { 
    server: string; 
    period: number;
  }
}

window.server = "https://localhost:5001";
window.period = 1000;

export interface AppProps {
  title: string;
  isOfficeInitialized: boolean;
}

export interface AppState {
  listItems: HeroListItem[];
  branch: string,
}

export default class App extends React.Component<AppProps, AppState> {
  constructor(props, context) {
    super(props, context);
    this.state = {
      listItems: [],
      branch: ""
    };
  }
  
  componentDidMount() {
    this.setState({
      listItems: [ ],
      branch: "" 
    });
  }

  ExcelDateToJSDate = (serial : number) => {
    var utc_days  = Math.floor(serial - 25569);
    var utc_value = utc_days * 86400;
    var date_info = new Date(utc_value * 1000);

    var fractional_day = serial - Math.floor(serial) + 0.0000001;

    var total_seconds = Math.floor(86400 * fractional_day);

    var seconds = total_seconds % 60;

    total_seconds -= seconds;

    var hours = Math.floor(total_seconds / (60 * 60));
    var minutes = Math.floor(total_seconds / 60) % 60;

    return new Date(date_info.getFullYear(), date_info.getMonth(), date_info.getDate(), hours, minutes, seconds);
  }
  
  doRange = async(fn : (data : Excel.Range) => Promise<void>) => {
    try{
      await Excel.run(async context => {
        const range = context.workbook.getSelectedRange();

        // Read the range address
        range.load("address");
        range.load("values");

        await context.sync();

        try {
          await fn(range);
        } catch (error) {
          range.values[0][0] = JSON.stringify(error.message, undefined, 2);
          console.error(error);
        }

        await context.sync();
      });
    }
    catch(e) { console.error(e); }
  }
  
  activeBranch = async() => 
  {
    await this.doRange(this.activeBranchEx);
  }
  
  activeBranchEx = async() =>
  {
    const query = "query { activeBranch }";
    let data : any = await request(window.server, query)
    this.setState( { branch : data.activeBranch.toString() });
  }
  
  render() {
    const {
      title,
      isOfficeInitialized,
    } = this.props;

    if (!isOfficeInitialized) {
      return (
        <Progress
          title={title}
          logo='assets/logo-filled.png'
          message='Please sideload your addin to see app body.'
        />
      );
    }
    
    if (this.state.branch == "") {
      // @ts-ignore
      this.activeBranchEx();
    }
    
    // @ts-ignore
    //window.branch = "Test";
    
    return (
      <div className='ms-welcome'>
        <Header logo='assets/logo-filled.png' title={this.props.title} message='Welcome' />
        <HeroList message={this.state.branch} items={this.state.listItems}>
        </HeroList>
      </div>
    );
  }
}
